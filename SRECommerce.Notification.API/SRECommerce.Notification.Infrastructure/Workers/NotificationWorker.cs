using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Infrastructure;

public sealed class NotificationWorker(IServiceScopeFactory scopes, INotificationTransport transport, TimeProvider clock, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!transport.Available) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await Tick(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (DbUpdateConcurrencyException) { }
            catch (Exception ex) { logger.LogWarning("Notification processing will retry ({ErrorType})", ex.GetType().Name); }
        }
    }

    private async Task Tick(CancellationToken ct)
    {
        using var queryScope = scopes.CreateScope();
        var now = clock.GetUtcNow();
        var due = await queryScope.ServiceProvider.GetRequiredService<NotificationDbContext>().Jobs.IgnoreQueryFilters().AsNoTracking()
            .Where(x => (x.State == DeliveryState.Pending && x.NextAttemptAt <= now) ||
                        (x.State == DeliveryState.Sending && x.LeaseUntil <= now))
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.EventId).Take(8).Select(x => new { x.TenantId, x.EventId, x.Channel }).ToArrayAsync(ct);
        foreach (var candidate in due)
        {
            using var work = scopes.CreateScope();
            var services = work.ServiceProvider;
            var scope = services.GetRequiredService<ModuleScope>(); scope.BindEvent(candidate.TenantId, "Notification");
            var db = services.GetRequiredService<NotificationDbContext>();
            var contexts = services.GetRequiredService<IHttpContextAccessor>();
            var row = await db.Jobs.SingleAsync(x => x.EventId == candidate.EventId && x.Channel == candidate.Channel, ct);
            if (row.State == DeliveryState.Sending && row.LeaseUntil <= clock.GetUtcNow())
            {
                await Finish(db, scope, contexts, row, new(DeliveryState.NeedsReview, "DeliveryLeaseExpired"), ct);
                continue;
            }
            if (row.State != DeliveryState.Pending || row.NextAttemptAt > clock.GetUtcNow()) continue;
            string? reminderRecipient = null;
            if (row.Kind == NotificationKind.CartReminder)
            {
                try { reminderRecipient = await services.GetRequiredService<CartReminderDelivery>().EligibleRecipient(row, ct); }
                catch (Exception ex) when (ex is SRECommerce.BuildingBlocks.Contracts.DependencyFault or System.Text.Json.JsonException)
                {
                    row.Attempts++;
                    await Finish(db, scope, contexts, row, new(DeliveryState.Pending, "CartReminderCheckUnavailable", true), ct);
                    continue;
                }
            }
            var stockPermitted = true;
            if (row.Kind == NotificationKind.LowStock && row.UserId is {} manager)
            {
                var subscription = await db.StockSubscriptions.SingleOrDefaultAsync(x => x.UserId == manager, ct);
                var contact = subscription?.Enabled == true
                    ? await services.GetRequiredService<IManagerRecipients>().Find(row.TenantId, manager, ct) : null;
                stockPermitted = contact is not null;
                row.Recipient = contact?.Email;
            }
            row.Attempts++;
            var preference = row.UserId is {} user ? await db.Preferences.SingleOrDefaultAsync(x => x.UserId == user, ct) : null;
            if (!stockPermitted || preference is not null && !preference.Permits(row.Channel) ||
                row.Kind == NotificationKind.CartReminder && reminderRecipient is null)
            {
                await Finish(db, scope, contexts, row, new(DeliveryState.Suppressed, "OptedOut"), ct);
                continue;
            }
            row.State = DeliveryState.Sending; row.LeaseId = Guid.NewGuid(); row.LeaseUntil = clock.GetUtcNow().AddSeconds(45);
            await db.SaveChangesAsync(ct);
            if (row.Kind == NotificationKind.CartReminder) row.Recipient = reminderRecipient;
            var result = await transport.Send(row, ct);
            // If shutdown interrupts the final write, the persisted lease prevents blind duplicate sends.
            await Finish(db, scope, contexts, row, result, ct);
        }
    }

    private async Task Finish(NotificationDbContext db, ModuleScope scope, IHttpContextAccessor contexts, NotificationJob row, DeliveryResult result, CancellationToken ct)
    {
        row.State = result.Retry ? row.Attempts < 5 ? DeliveryState.Pending : DeliveryState.Failed : result.State;
        row.LeaseId = null; row.LeaseUntil = null;
        var now = clock.GetUtcNow();
        if (row.State == DeliveryState.Pending) row.NextAttemptAt = now.AddSeconds(10 * (1 << row.Attempts));
        if (row.State != DeliveryState.Pending || row.Kind == NotificationKind.CartReminder) row.Recipient = null;
        db.Deliveries.Add(new() { TenantId = row.TenantId, EventId = row.EventId, Channel = row.Channel, Attempt = row.Attempts, Code = result.Code, State = row.State, RecordedAt = now });
        db.Record(scope, contexts, "notification.delivery.recorded", row.EventId.ToString());
        await db.SaveChangesAsync(ct);
    }
}
