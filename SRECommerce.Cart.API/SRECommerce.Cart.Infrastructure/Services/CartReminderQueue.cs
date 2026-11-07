using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.Infrastructure;

public static class CartReminderEligibility
{
    public static IQueryable<CartReminder> Due(CartDbContext db, DateTimeOffset now)
    {
        var cutoff = now.AddDays(-1);
        return db.Reminders.Where(row => row.Enabled && row.RequestedAt <= cutoff && row.Cart.UserId != null &&
            row.Cart.Status == CartStatus.Active && row.Cart.UpdatedAt <= cutoff && row.Cart.ExpiresAt > now &&
            row.Cart.Items.Any(item => item.Active) && !db.Claims.Any(claim => claim.TenantId == row.TenantId &&
                claim.CartId == row.CartId && claim.Status == CartClaimStatus.Held));
    }
}

public sealed class CartReminderQueue(IServiceScopeFactory scopes, TimeProvider clock)
{
    public async Task QueueDue(CancellationToken ct)
    {
        using var query = scopes.CreateScope();
        var candidates = await CartReminderEligibility.Due(query.ServiceProvider.GetRequiredService<CartDbContext>(), clock.GetUtcNow())
            .IgnoreQueryFilters().Where(row => row.QueuedEventId == null)
            .OrderBy(row => row.RequestedAt).ThenBy(row => row.CartId).Take(16)
            .Select(row => new { row.TenantId, row.CartId }).ToArrayAsync(ct);
        foreach (var candidate in candidates)
        {
            using var work = scopes.CreateScope();
            var services = work.ServiceProvider;
            var scope = services.GetRequiredService<ModuleScope>();
            scope.BindEvent(candidate.TenantId, "Cart");
            var db = services.GetRequiredService<CartDbContext>();
            var contexts = services.GetRequiredService<IHttpContextAccessor>();
            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var reminder = await CartReminderEligibility.Due(db, now).Include(row => row.Cart)
                .SingleOrDefaultAsync(row => row.CartId == candidate.CartId && row.QueuedEventId == null, ct);
            if (reminder is null) continue;
            reminder.QueuedEventId = db.Emit(scope, ServiceManifest.Get("Cart"), contexts, MessagingTopology.CartReminderDue,
                new CartReminderEvent(reminder.CartId, reminder.Cart.UserId!.Value));
            reminder.QueuedAt = now;
            db.Record(scope, contexts, "cart.reminder.queued", reminder.CartId.ToString());
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}

public sealed class CartReminderWorker(CartReminderQueue queue, IConfiguration configuration, TimeProvider clock,
    ILogger<CartReminderWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("CartReminders:Enabled") || !configuration.GetValue<bool>("Messaging:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await queue.QueueDue(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Cart reminder scheduling will retry ({ErrorType})", ex.GetType().Name); }
        }
    }
}
