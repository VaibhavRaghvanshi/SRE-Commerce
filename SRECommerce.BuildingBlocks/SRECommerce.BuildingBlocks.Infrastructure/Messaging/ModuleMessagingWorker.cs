using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public sealed class ModuleMessagingWorker(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    IHostEnvironment environment,
    ServiceSpec module,
    ILogger<ModuleMessagingWorker> logger) : BackgroundService
{
    private const string RetryHeader = "srecommerce-retry";
    private const string RoutingHeader = "srecommerce-routing-key";
    private readonly string[] publications = MessagingTopology.Publications(module.Name);
    private readonly string[] subscriptions = MessagingTopology.Subscriptions(module.Name);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Messaging:Enabled") || publications.Length + subscriptions.Length == 0) return;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var factory = ConnectionFactory();
                await using var connection = await factory.CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), stoppingToken);
                await Configure(channel, stoppingToken);
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
                do
                {
                    await PublishPending(channel, stoppingToken);
                    await ConsumePending(channel, stoppingToken);
                } while (await timer.WaitForNextTickAsync(stoppingToken));
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error)
            {
                logger.LogWarning("Messaging connection will retry ({FailureType})", error.GetType().Name);
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    private ConnectionFactory ConnectionFactory()
    {
        var host = configuration["Messaging:Host"] ?? "localhost";
        var username = configuration["Messaging:Username"];
        var password = configuration["Messaging:Password"];
        var tls = configuration.GetValue<bool>("Messaging:Tls");
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Explicit service broker credentials required");
        if (!environment.IsDevelopment() && !tls)
            throw new InvalidOperationException("Production broker TLS required");
        return new ConnectionFactory
        {
            HostName = host,
            Port = configuration.GetValue("Messaging:Port", tls ? 5671 : 5672),
            VirtualHost = configuration["Messaging:VirtualHost"] ?? "srecommerce",
            UserName = username,
            Password = password,
            ClientProvidedName = "SRECommerce." + module.Name,
            AutomaticRecoveryEnabled = false,
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
            ContinuationTimeout = TimeSpan.FromSeconds(10),
            MaxInboundMessageBodySize = 262144,
            Ssl = new SslOption { Enabled = tls, ServerName = host }
        };
    }

    private async Task Configure(IChannel channel, CancellationToken ct)
    {
        await channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic, durable: true, cancellationToken: ct);
        if (subscriptions.Length == 0) return;
        var delivery = MessagingTopology.DeliveryExchange(module.Name);
        var retry = MessagingTopology.RetryExchange(module.Name);
        var dead = MessagingTopology.DeadExchange(module.Name);
        var queue = MessagingTopology.Queue(module.Name);
        foreach (var exchange in new[] { delivery, retry, dead })
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, durable: true, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        await channel.QueueBindAsync(queue, delivery, "events", cancellationToken: ct);
        foreach (var topic in subscriptions)
            await channel.QueueBindAsync(queue, MessagingTopology.EventsExchange, topic, cancellationToken: ct);
        await channel.QueueDeclareAsync(queue + ".retry", durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> {
                ["x-queue-type"] = "quorum",
                ["x-dead-letter-strategy"] = "at-least-once",
                ["x-overflow"] = "reject-publish",
                ["x-message-ttl"] = 30000,
                ["x-dead-letter-exchange"] = delivery,
                ["x-dead-letter-routing-key"] = "events"
            }, cancellationToken: ct);
        await channel.QueueBindAsync(queue + ".retry", retry, "retry", cancellationToken: ct);
        await channel.QueueDeclareAsync(queue + ".dead", durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-queue-type"] = "quorum" }, cancellationToken: ct);
        await channel.QueueBindAsync(queue + ".dead", dead, "dead", cancellationToken: ct);
    }

    private async Task PublishPending(IChannel channel, CancellationToken ct)
    {
        if (publications.Length == 0) return;
        using var query = scopes.CreateScope();
        var now = DateTimeOffset.UtcNow;
        var pending = await query.ServiceProvider.GetRequiredService<IModuleStore>().Outbox.IgnoreQueryFilters().AsNoTracking()
            .Where(row => publications.Contains(row.Type) && row.PublishedAt == null && row.NextAttemptAt <= now &&
                (row.LeaseUntil == null || row.LeaseUntil <= now))
            .OrderBy(row => row.NextAttemptAt).ThenBy(row => row.Id).Take(8)
            .Select(row => new { row.Id, row.TenantId }).ToArrayAsync(ct);
        foreach (var candidate in pending)
        {
            using var scope = scopes.CreateScope();
            scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(candidate.TenantId, module.Name);
            var store = scope.ServiceProvider.GetRequiredService<IModuleStore>();
            var row = await store.Outbox.SingleAsync(value => value.Id == candidate.Id, ct);
            if (row.PublishedAt is not null || row.LeaseUntil > DateTimeOffset.UtcNow) continue;
            row.LeaseId = Guid.NewGuid();
            row.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(1);
            try { await store.Context.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { continue; }
            var envelope = ContractJson.Read<EventEnvelope>(row.Envelope);
            if (envelope.Id != row.Id || envelope.TenantId != row.TenantId || envelope.Producer != module.Name || envelope.Type != row.Type)
                throw new InvalidOperationException("Invalid persisted event envelope");
            try
            {
                var properties = Properties(envelope, 0);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
                deadline.CancelAfter(TimeSpan.FromSeconds(15));
                await channel.BasicPublishAsync(MessagingTopology.EventsExchange, envelope.Type, true, properties,
                    Encoding.UTF8.GetBytes(row.Envelope), deadline.Token);
                row.PublishedAt = DateTimeOffset.UtcNow;
                row.LeaseId = null;
                row.LeaseUntil = null;
                await store.Context.SaveChangesAsync(ct);
            }
            catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                row.Attempts = Math.Min(1000000, row.Attempts + 1);
                row.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 5 * (1 << Math.Min(row.Attempts, 6))));
                row.LeaseId = null;
                row.LeaseUntil = null;
                await store.Context.SaveChangesAsync(ct);
                throw;
            }
        }
    }

    private static BasicProperties Properties(EventEnvelope envelope, int retry) => new()
    {
        MessageId = envelope.Id.ToString(),
        Type = envelope.Type,
        ContentType = "application/json",
        CorrelationId = envelope.CorrelationId,
        Persistent = true,
        Headers = new Dictionary<string, object?> { [RetryHeader] = retry, [RoutingHeader] = envelope.Type }
    };

    private static int Retry(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers?.TryGetValue(RetryHeader, out var value) != true) return 0;
        return value switch { int count when count is >= 0 and <= 5 => count, long count when count is >= 0 and <= 5 => (int)count, _ => 5 };
    }

    private async Task ConsumePending(IChannel channel, CancellationToken ct)
    {
        if (subscriptions.Length == 0) return;
        for (var count = 0; count < 8; count++)
        {
            var delivery = await channel.BasicGetAsync(MessagingTopology.Queue(module.Name), autoAck: false, ct);
            if (delivery is null) return;
            EventEnvelope? envelope = null;
            var retry = Retry(delivery.BasicProperties);
            try
            {
                if (delivery.Body.Length is 0 or > 262144) throw new BusinessFault("InvalidEventSize", 400);
                envelope = ContractJson.Read<EventEnvelope>(Encoding.UTF8.GetString(delivery.Body.Span));
                var expectedProducer = envelope.Type.Split('.')[0];
                if (envelope.Id == Guid.Empty || envelope.TenantId == Guid.Empty || envelope.SchemaVersion != 1 ||
                    !subscriptions.Contains(envelope.Type) || envelope.Producer.ToLowerInvariant() != expectedProducer ||
                    !ServiceManifest.All.Any(service => service.Name == envelope.Producer) ||
                    delivery.BasicProperties.MessageId != envelope.Id.ToString() || delivery.BasicProperties.Type != envelope.Type)
                    throw new BusinessFault("InvalidEventIdentity", 400);
                var initial = delivery.Exchange == MessagingTopology.EventsExchange && delivery.RoutingKey == envelope.Type;
                var retryDelivery = delivery.Exchange == MessagingTopology.DeliveryExchange(module.Name) && delivery.RoutingKey == "events";
                if (!initial && !retryDelivery) throw new BusinessFault("InvalidEventRoute", 400);
                using var scope = scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(envelope.TenantId, envelope.Producer);
                var handler = scope.ServiceProvider.GetServices<IIntegrationHandler>().SingleOrDefault(candidate => candidate.EventTypes.Contains(envelope.Type))
                    ?? throw new BusinessFault("UnsupportedIntegrationEvent", 400);
                await handler.HandleAsync(envelope, ct);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception error)
            {
                var permanent = error is JsonException or BusinessFault { Status: 400 or 403 } or InvalidOperationException;
                var dead = permanent || retry >= 5;
                var properties = envelope is null ? new BasicProperties { Persistent = true } : Properties(envelope, retry + 1);
                properties.Headers ??= new Dictionary<string, object?>();
                properties.Headers["srecommerce-failure"] = error.GetType().Name;
                await channel.BasicPublishAsync(
                    dead ? MessagingTopology.DeadExchange(module.Name) : MessagingTopology.RetryExchange(module.Name),
                    dead ? "dead" : "retry", true, properties, delivery.Body.ToArray(), ct);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, ct);
                logger.LogWarning("Integration delivery moved to {Destination} ({FailureType})", dead ? "dead letter queue" : "retry queue", error.GetType().Name);
            }
        }
    }
}
