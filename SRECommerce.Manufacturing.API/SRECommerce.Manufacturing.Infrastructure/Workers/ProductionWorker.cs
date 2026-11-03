using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Manufacturing.Application;
using SRECommerce.Manufacturing.Domain;

namespace SRECommerce.Manufacturing.Infrastructure;

public sealed class ProductionWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<ProductionWorker> logger) : BackgroundService
{
    private static readonly ProductionState[] Work = [ProductionState.Reserving, ProductionState.Consuming, ProductionState.Receiving, ProductionState.Cancelling];
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(3), clock);
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await RunOnce(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (DbUpdateConcurrencyException) { }
            catch (Exception e) { logger.LogWarning("Production processing deferred ({ErrorType})", e.GetType().Name); }
        }
    }

    public async Task RunOnce(CancellationToken ct)
    {
        using var query = scopes.CreateScope();
        var now = clock.GetUtcNow();
        var work = await query.ServiceProvider.GetRequiredService<ManufacturingDbContext>().Orders.IgnoreQueryFilters().AsNoTracking()
            .Where(x => Work.Contains(x.State) && x.NextAttemptAt <= now && (x.LeaseUntil == null || x.LeaseUntil <= now))
            .OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id).Take(5).Select(x => new { x.TenantId, x.Id }).ToArrayAsync(ct);
        foreach (var candidate in work)
        {
            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            var tenant = services.GetRequiredService<ModuleScope>(); tenant.BindEvent(candidate.TenantId, "Manufacturing");
            var db = services.GetRequiredService<ManufacturingDbContext>();
            var order = await db.Orders.SingleAsync(x => x.Id == candidate.Id, ct);
            if (!Work.Contains(order.State) || order.LeaseUntil > clock.GetUtcNow() || order.NextAttemptAt > clock.GetUtcNow()) continue;
            order.LeaseId = Guid.NewGuid(); order.LeaseUntil = clock.GetUtcNow().AddSeconds(45);
            await db.SaveChangesAsync(ct);
            var from = order.State;
            string action;
            try
            {
                var inventory = services.GetRequiredService<IManufacturingInventory>();
                switch (from)
                {
                    case ProductionState.Reserving:
                        var held = await inventory.Reserve(order, ContractJson.Read<ProductionMaterial[]>(order.Materials), ct);
                        if (held.Status is not (0 or 1 or 2 or 3 or 4)) throw new DependencyFault("Inventory");
                        order.State = held.Status is 0 or 1 ? ProductionState.Consuming : ProductionState.Cancelled;
                        action = order.State == ProductionState.Consuming ? "materials-reserved" : "reservation-closed"; break;
                    case ProductionState.Consuming:
                        var committed = await inventory.Consume(order, ct);
                        if (committed.Status is not (1 or 2 or 3 or 4)) throw new DependencyFault("Inventory");
                        order.InputsConsumed = committed.Status == 1;
                        order.State = order.InputsConsumed ? ProductionState.Running : ProductionState.Cancelled;
                        action = order.InputsConsumed ? "materials-consumed" : "reservation-closed"; break;
                    case ProductionState.Receiving:
                        await inventory.Receive(order, ct); order.State = ProductionState.Completed; action = "finished-stock-received"; break;
                    case ProductionState.Cancelling:
                        await inventory.Cancel(order, ct); order.State = ProductionState.Cancelled; action = "cancelled"; break;
                    default: throw new InvalidOperationException("Unexpected production state");
                }
                order.Attempts = 0; order.LastFault = null; order.NextAttemptAt = clock.GetUtcNow();
            }
            catch (Exception e) when (e is BusinessFault or DependencyFault or HttpRequestException)
            {
                order.Attempts++;
                order.LastFault = e is BusinessFault rule ? rule.Code : "InventoryUnavailable";
                order.NextAttemptAt = clock.GetUtcNow().AddSeconds(Math.Min(300, 5 * (1 << Math.Min(order.Attempts, 6))));
                if (order.Attempts >= 8 || e is BusinessFault { Status: 400 or 403 or 404 })
                { order.State = ProductionState.NeedsReview; order.ResumeState = from; action = "needs-review"; }
                else action = "retry-scheduled";
            }
            order.LeaseId = null; order.LeaseUntil = null;
            db.History.Add(new ProductionHistory { TenantId = order.TenantId, Id = Guid.NewGuid(), OrderId = order.Id,
                State = order.State, Action = action, Note = order.LastFault ?? "", CreatedAt = clock.GetUtcNow() });
            db.Record(tenant, services.GetRequiredService<IHttpContextAccessor>(), "manufacturing.production." + action, order.Id.ToString());
            await db.SaveChangesAsync(ct);
        }
    }
}
