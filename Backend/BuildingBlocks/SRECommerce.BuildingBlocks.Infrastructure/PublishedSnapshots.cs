using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public sealed record PublishedSnapshot(long Sequence,string Document,string Hash,DateTimeOffset PublishedAt);
public interface IPublishedSnapshots
{
    Task<PublishedSnapshot?> GetAsync(Guid tenant,string kind,CancellationToken ct);
    Task PutAsync(Guid tenant,string kind,PublishedSnapshot snapshot,CancellationToken ct);
}
public sealed class PublishedSnapshots(ServiceSpec module,IConfiguration configuration,ILogger<PublishedSnapshots> logger):IPublishedSnapshots,IDisposable
{
    private readonly MemoryCache memory=new(new MemoryCacheOptions{SizeLimit=512});
    private readonly SemaphoreSlim connectionGate=new(1,1);
    private readonly SemaphoreSlim[] locks=Enumerable.Range(0,64).Select(_=>new SemaphoreSlim(1,1)).ToArray();
    private IConnectionMultiplexer? connection;
    private static readonly TimeSpan Lifetime=TimeSpan.FromMinutes(10);
    private string Key(Guid tenant,string kind)
    {if(tenant==Guid.Empty||kind.Length is <1 or >40||kind.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!='-'))throw new ArgumentException("Invalid snapshot key");return $"SRECommerce:{module.Name}:v1:tenant:{tenant}:published:{kind}";}
    private async Task<IConnectionMultiplexer?> Redis(CancellationToken ct)
    {
        if(!configuration.GetValue<bool>("Cache:Enabled"))return null;
        if(connection is not null)return connection;
        await connectionGate.WaitAsync(ct);
        try
        {
            if(connection is null)
            {
                var options=new ConfigurationOptions{AbortOnConnectFail=false,ConnectTimeout=1000,SyncTimeout=1000,AsyncTimeout=1000,ConnectRetry=1,Password=configuration["Cache:Password"],User=configuration["Cache:User"],ClientName="SRECommerce."+module.Name};
                options.EndPoints.Add(configuration["Cache:Host"]??"localhost",configuration.GetValue("Cache:Port",6379));
                connection=await ConnectionMultiplexer.ConnectAsync(options);
            }
            return connection;
        }
        finally{connectionGate.Release();}
    }
    public async Task<PublishedSnapshot?> GetAsync(Guid tenant,string kind,CancellationToken ct)
    {
        var key=Key(tenant,kind);memory.TryGetValue<PublishedSnapshot>(key,out var cached);
        try
        {
            if(await Redis(ct) is {} redis)
            {
                var raw=await redis.GetDatabase().HashGetAsync(key,"payload");
                if(!raw.IsNull&&raw.Length()<=262144)
                {
                    var value=ContractJson.Read<PublishedSnapshot>(raw.ToString());
                    if(cached is null||value.Sequence>=cached.Sequence){memory.Set(key,value,new MemoryCacheEntryOptions{Size=1,AbsoluteExpirationRelativeToNow=Lifetime});return value;}
                }
            }
        }
        catch(Exception ex)when(ex is RedisException or TimeoutException or System.Text.Json.JsonException or BusinessFault)
        {logger.LogWarning("Public snapshot cache read unavailable ({ErrorType})",ex.GetType().Name);}
        return cached;
    }
    public async Task PutAsync(Guid tenant,string kind,PublishedSnapshot value,CancellationToken ct)
    {
        var key=Key(tenant,kind);var stripe=locks[(key.GetHashCode()&int.MaxValue)%locks.Length];await stripe.WaitAsync(ct);
        try
        {
            if(memory.TryGetValue<PublishedSnapshot>(key,out var prior)&&prior!.Sequence>value.Sequence)return;
            memory.Set(key,value,new MemoryCacheEntryOptions{Size=1,AbsoluteExpirationRelativeToNow=Lifetime});
            try
            {
                if(await Redis(ct) is {} redis)
                {
                    const string script="local old=redis.call('HGET',KEYS[1],'sequence');if not old or tonumber(ARGV[1])>=tonumber(old) then redis.call('HSET',KEYS[1],'sequence',ARGV[1],'payload',ARGV[2]);redis.call('EXPIRE',KEYS[1],ARGV[3]);return 1 else return 0 end";
                    await redis.GetDatabase().ScriptEvaluateAsync(script,[key],[value.Sequence,ContractJson.Write(value),(long)Lifetime.TotalSeconds]);
                }
            }
            catch(Exception ex)when(ex is RedisException or TimeoutException){logger.LogWarning("Public snapshot cache write deferred ({ErrorType})",ex.GetType().Name);}
        }
        finally{stripe.Release();}
    }
    public void Dispose(){connection?.Dispose();memory.Dispose();connectionGate.Dispose();foreach(var value in locks)value.Dispose();}
}
