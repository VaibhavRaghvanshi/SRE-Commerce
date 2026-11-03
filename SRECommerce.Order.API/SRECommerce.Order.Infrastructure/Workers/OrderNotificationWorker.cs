using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;

namespace SRECommerce.Order.Infrastructure;

public sealed class OrderNotificationWorker(IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider clock, ILogger<OrderNotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Notifications:DispatchEnabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await Dispatch(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (DbUpdateConcurrencyException) { }
            catch (Exception ex) { logger.LogWarning("Order notice dispatch deferred ({ErrorType})", ex.GetType().Name); }
        }
    }

    private async Task Dispatch(CancellationToken ct)
    {
        using var query = scopes.CreateScope();
        var now = clock.GetUtcNow();
        var batch = await query.ServiceProvider.GetRequiredService<OrderDbContext>().Outbox.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Type.StartsWith(OrderNoticesContract.Prefix) && x.PublishedAt == null && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil <= now))
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(5).Select(x => new { x.TenantId, x.Id }).ToArrayAsync(ct);
        foreach (var item in batch)
        {
            using var work = scopes.CreateScope();
            var services = work.ServiceProvider;
            services.GetRequiredService<ModuleScope>().BindEvent(item.TenantId, "Order");
            var db = services.GetRequiredService<OrderDbContext>();
            var row = await db.Outbox.SingleAsync(x => x.Id == item.Id, ct);
            if (row.PublishedAt is not null || row.LeaseUntil > clock.GetUtcNow()) continue;
            var kind = Array.IndexOf(OrderNoticesContract.Types, row.Type);
            var notice = ContractJson.Read<EventEnvelope>(row.Envelope).Data.Deserialize<OrderNoticeResource>(ContractJson.Strict);
            if (kind < 0 || notice is null) continue;
            row.LeaseId = Guid.NewGuid(); row.LeaseUntil = clock.GetUtcNow().AddSeconds(30);
            await db.SaveChangesAsync(ct);
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, $"internal/v1/tenants/{item.TenantId}/notifications/order")
                { Content = JsonContent.Create(new { eventId = item.Id, notice.OrderId, kind }) };
                using var response = await services.GetRequiredService<ServiceHttp>().Send("Notification", request, ct);
                if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.NotFound) row.PublishedAt = clock.GetUtcNow();
            }
            catch (Exception ex) when (ex is HttpRequestException or DependencyFault) { }
            row.LeaseId = null; row.LeaseUntil = null;
            if (row.PublishedAt is null)
            {
                row.Attempts = Math.Min(row.Attempts + 1, 20);
                row.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(1800, 10 * (1 << Math.Min(row.Attempts, 8))));
            }
            await db.SaveChangesAsync(ct);
        }
    }
}
