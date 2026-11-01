using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public interface IServiceTokens
{
    Task<string> Get(string target,CancellationToken ct);
}

// Machine tokens are private process memory. Never use a browser token for durable recovery.
public sealed class ServiceTokens(ServiceSpec source,IConfiguration config,IHttpClientFactory clients,TimeProvider clock):IServiceTokens,IDisposable
{
    private sealed record Cached(string Token,DateTimeOffset RefreshAt);
    private readonly ConcurrentDictionary<string,Cached> tokens=new(StringComparer.Ordinal);
    private readonly SemaphoreSlim refresh=new(1,1);

    public async Task<string> Get(string target,CancellationToken ct)
    {
        if(!ServiceManifest.Targets(source.Name).Contains(target,StringComparer.Ordinal))throw new BusinessFault("ServiceTargetNotAllowed",403);
        if(tokens.TryGetValue(target,out var cached)&&cached.RefreshAt>clock.GetUtcNow())return cached.Token;
        await refresh.WaitAsync(ct);
        try
        {
            if(tokens.TryGetValue(target,out cached)&&cached.RefreshAt>clock.GetUtcNow())return cached.Token;
            var id=config["ServiceAuth:ClientId"]??ServiceManifest.ClientId(source.Name);
            var secret=config["ServiceAuth:ClientSecret"];
            if(id!=ServiceManifest.ClientId(source.Name)||string.IsNullOrWhiteSpace(secret))throw new DependencyFault("ServiceIdentity");
            using var request=new HttpRequestMessage(HttpMethod.Post,"connect/token")
            {Content=new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","client_credentials"},{"client_id",id},{"client_secret",secret},{"scope",ServiceManifest.Get(target).InternalScope}})};
            HttpResponseMessage response;
            try{response=await clients.CreateClient("service-identity").SendAsync(request,ct);}
            catch(Exception ex)when(ex is HttpRequestException or Polly.Timeout.TimeoutRejectedException or Polly.CircuitBreaker.BrokenCircuitException){throw new DependencyFault("ServiceIdentity");}
            catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new DependencyFault("ServiceIdentity");}
            using(response)
            {
                if(!response.IsSuccessStatusCode)throw new DependencyFault("ServiceIdentity");
                try
                {
                    var document=await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
                    var token=document.GetProperty("access_token").GetString();var seconds=document.GetProperty("expires_in").GetInt32();
                    if(string.IsNullOrEmpty(token)||token.Length>16384||seconds<=0||!document.GetProperty("token_type").GetString()!.Equals("Bearer",StringComparison.OrdinalIgnoreCase))throw new DependencyFault("ServiceIdentity");
                    tokens[target]=new(token,clock.GetUtcNow().AddSeconds(Math.Clamp(seconds-15,0,300)));return token;
                }
                catch(Exception ex)when(ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException or NullReferenceException){throw new DependencyFault("ServiceIdentity");}
            }
        }
        finally{refresh.Release();}
    }
    public void Dispose()=>refresh.Dispose();
}
