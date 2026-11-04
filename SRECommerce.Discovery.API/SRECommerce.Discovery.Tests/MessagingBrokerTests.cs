using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Discovery.Application;
using SRECommerce.Discovery.Infrastructure;
using Xunit;

namespace SRECommerce.Discovery.Tests;

public sealed class BrokerFactAttribute : FactAttribute
{
    public BrokerFactAttribute()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SRECOMMERCE_TEST_BROKER_PORT")))
            Skip = "Opt-in isolated RabbitMQ test: provide SRECOMMERCE_TEST_BROKER_PORT/USER/PASSWORD.";
    }
}

public sealed class MessagingBrokerTests
{
    public sealed class OutboxDb(DbContextOptions<OutboxDb> options, ModuleScope scope) : DbContext(options), IModuleStore
    {
        public DbContext Context => this;
        public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
        public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
        public DbSet<AuditEntry> Audit => Set<AuditEntry>();
        protected override void OnModelCreating(ModelBuilder m) => m.ConfigureInfrastructure();
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
        { this.GuardWrites(scope); return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct); }
    }
    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "SRECommerce.BrokerTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    private sealed class Access : IDiscoveryAccess
    {
        public Task Public(Guid tenant, CancellationToken ct) => Task.CompletedTask;
        public Task<Guid> Human(Guid tenant, ActorPermission role, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class Sources : IDiscoverySources
    {
        public Task<IndexPage> CatalogPage(Guid tenant, int page, CancellationToken ct) => throw new NotSupportedException();
        public Task<IndexProduct[]> PublicProducts(Guid tenant, Guid[] products, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> VerifiedPurchase(Guid tenant, Guid user, Guid product, CancellationToken ct) => throw new NotSupportedException();
    }
    private sealed class DeliveryState { public int Attempts; }
    private sealed class RetryOnce(CatalogProjectionHandler handler, DeliveryState state) : IIntegrationHandler
    {
        public IReadOnlyCollection<string> EventTypes => MessagingTopology.CatalogEvents;
        public Task HandleAsync(EventEnvelope message, CancellationToken ct)
        {
            if (Interlocked.Increment(ref state.Attempts) == 1) throw new DependencyFault("IsolatedFirstDelivery");
            return handler.HandleAsync(message, ct);
        }
    }
    private static string Connection(string service, string suffix) => ModuleDatabase.Check(ServiceManifest.Get(service),
        $"Server=localhost,1433;Database=SRECommerce_{service}Tests_Broker_{suffix};Integrated Security=true;Encrypt=true;TrustServerCertificate=true", true);
    private static ModuleMessagingWorker Worker(ServiceProvider provider, IConfiguration config, string service) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), config, new TestEnvironment(), ServiceManifest.Get(service), NullLogger<ModuleMessagingWorker>.Instance);
    private static async Task Until(Func<Task<bool>> condition, CancellationToken ct)
    {
        while (!await condition()) await Task.Delay(250, ct);
    }

    [BrokerFact]
    public async Task Confirmed_outbox_reaches_SQL_inbox_with_isolated_retry_duplicate_deduplication_and_poison_DLQ()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(2)); var ct = deadline.Token;
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Messaging:Enabled"]="true", ["Messaging:Host"]="localhost",
            ["Messaging:Port"]=Environment.GetEnvironmentVariable("SRECOMMERCE_TEST_BROKER_PORT"),
            ["Messaging:Username"]=Environment.GetEnvironmentVariable("SRECOMMERCE_TEST_BROKER_USER"),
            ["Messaging:Password"]=Environment.GetEnvironmentVariable("SRECOMMERCE_TEST_BROKER_PASSWORD"),
            ["Messaging:VirtualHost"]="srecommerce-tests"
        }).Build();
        var suffix=Guid.NewGuid().ToString("N"); var tenant=Guid.NewGuid();
        var pubServices=new ServiceCollection();pubServices.AddScoped<ModuleScope>();
        pubServices.AddDbContext<OutboxDb>(o=>o.UseSqlServer(Connection("Catalog",suffix)));
        pubServices.AddScoped<IModuleStore>(p=>p.GetRequiredService<OutboxDb>());
        await using var publisher=pubServices.BuildServiceProvider();
        var subServices=new ServiceCollection();subServices.AddScoped<ModuleScope>();
        subServices.AddDbContext<DiscoveryDbContext>(o=>o.UseSqlServer(Connection("Discovery",suffix)));
        subServices.AddScoped<IModuleStore>(p=>p.GetRequiredService<DiscoveryDbContext>());
        subServices.AddScoped<IDiscoveryStore,DiscoveryStore>();subServices.AddSingleton<IHttpContextAccessor,HttpContextAccessor>();
        subServices.AddSingleton<IDiscoveryAccess,Access>();subServices.AddSingleton<IDiscoverySources,Sources>();subServices.AddSingleton(TimeProvider.System);
        subServices.AddScoped<DiscoveryService>();subServices.AddScoped<CatalogProjectionHandler>();
        subServices.AddSingleton<DeliveryState>();subServices.AddScoped<IIntegrationHandler,RetryOnce>();
        await using var subscriber=subServices.BuildServiceProvider();
        using(var scope=publisher.CreateScope())await scope.ServiceProvider.GetRequiredService<OutboxDb>().Database.EnsureCreatedAsync(ct);
        using(var scope=subscriber.CreateScope())await scope.ServiceProvider.GetRequiredService<DiscoveryDbContext>().Database.MigrateAsync(ct);
        var product=new IndexProduct(Guid.NewGuid(),"Broker projection","broker-projection",Guid.NewGuid(),null,"Confirmed transport",true,false,[],[new("BROKER-1",true)],1);
        var envelope=new EventEnvelope(Guid.NewGuid(),1,"Catalog","catalog.product.published.v1",tenant,"broker-test",DateTimeOffset.UtcNow,JsonSerializer.SerializeToElement(product,ContractJson.Strict));
        using(var scope=publisher.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenant,"Catalog");var db=scope.ServiceProvider.GetRequiredService<OutboxDb>();
            db.Outbox.Add(new(){Id=envelope.Id,TenantId=tenant,Type=envelope.Type,Envelope=ContractJson.Write(envelope)});await db.SaveChangesAsync(ct);
        }
        using var sub=Worker(subscriber,config,"Discovery");using var pub=Worker(publisher,config,"Catalog");
        var factory=new ConnectionFactory{HostName="localhost",Port=int.Parse(config["Messaging:Port"]!),UserName=config["Messaging:Username"]!,Password=config["Messaging:Password"]!,VirtualHost="srecommerce-tests"};
        await using var connection=await factory.CreateConnectionAsync(ct);await using var channel=await connection.CreateChannelAsync(new CreateChannelOptions(true,true),ct);
        await sub.StartAsync(ct);
        // If topology startup overlaps, mandatory returns leave the outbox pending.
        await pub.StartAsync(ct);
        try
        {
            await Until(async()=>{using var scope=subscriber.CreateScope();return await scope.ServiceProvider.GetRequiredService<DiscoveryDbContext>().Inbox.IgnoreQueryFilters().AnyAsync(x=>x.Id==envelope.Id,ct);},ct);
            Assert.True(subscriber.GetRequiredService<DeliveryState>().Attempts>=2);
            using(var scope=publisher.CreateScope())Assert.NotNull((await scope.ServiceProvider.GetRequiredService<OutboxDb>().Outbox.SingleAsync(ct)).PublishedAt);
            await channel.BasicPublishAsync(MessagingTopology.EventsExchange,envelope.Type,true,new BasicProperties{Persistent=true,MessageId=envelope.Id.ToString(),Type=envelope.Type},System.Text.Encoding.UTF8.GetBytes(ContractJson.Write(envelope)),ct);
            var poison=envelope with{Id=Guid.NewGuid(),SchemaVersion=9};
            await channel.BasicPublishAsync(MessagingTopology.EventsExchange,poison.Type,true,new BasicProperties{Persistent=true,MessageId=poison.Id.ToString(),Type=poison.Type},System.Text.Encoding.UTF8.GetBytes(ContractJson.Write(poison)),ct);
            BasicGetResult? dead=null;
            await Until(async()=>{dead=await channel.BasicGetAsync(MessagingTopology.Queue("Discovery")+".dead",true,ct);return dead is not null;},ct);
            Assert.Equal(poison.Id,ContractJson.Read<EventEnvelope>(System.Text.Encoding.UTF8.GetString(dead!.Body.Span)).Id);
            using var final=subscriber.CreateScope();var db=final.ServiceProvider.GetRequiredService<DiscoveryDbContext>();
            Assert.Single(await db.Inbox.IgnoreQueryFilters().ToArrayAsync(ct));Assert.Single(await db.Documents.IgnoreQueryFilters().ToArrayAsync(ct));
        }
        finally
        {
            using var shutdown=new CancellationTokenSource(TimeSpan.FromSeconds(10));await pub.StopAsync(shutdown.Token);await sub.StopAsync(shutdown.Token);
        }
    }
}
