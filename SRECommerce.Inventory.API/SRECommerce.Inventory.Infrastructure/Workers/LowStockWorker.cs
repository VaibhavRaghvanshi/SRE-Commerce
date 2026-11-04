using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Inventory.Domain;

namespace SRECommerce.Inventory.Infrastructure;

public sealed class LowStockWorker(IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider clock, ILogger<LowStockWorker> logger) : BackgroundService
{
    private int offset;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("LowStock:Enabled")) return;
        var seconds = Math.Clamp(configuration.GetValue("LowStock:ScanSeconds", 30), 15, 300);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(seconds));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { await Scan(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogWarning("Low stock scan deferred ({FailureType})", error.GetType().Name); }
        }
    }

    public async Task Scan(CancellationToken ct)
    {
        using var query = scopes.CreateScope();
        var candidates = await query.ServiceProvider.GetRequiredService<InventoryDbContext>().Items.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Active).OrderBy(item => item.TenantId).ThenBy(item => item.Id)
            .Skip(offset).Take(100).Select(item => new { item.TenantId, item.Id }).ToArrayAsync(ct);
        offset = candidates.Length == 100 ? offset + 100 : 0;
        foreach (var group in candidates.GroupBy(item => item.TenantId))
        {
            using var scope = scopes.CreateScope();
            var services = scope.ServiceProvider;
            var tenantScope = services.GetRequiredService<ModuleScope>();
            tenantScope.BindEvent(group.Key, "Inventory");
            var db = services.GetRequiredService<InventoryDbContext>();
            var variants = group.Select(item => item.Id).ToArray();
            var now = clock.GetUtcNow();
            await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
            var items = await db.Items.Where(item => variants.Contains(item.Id) && item.Active).ToArrayAsync(ct);
            var balances = await (
                from balance in db.Balances
                join batch in db.Batches on balance.BatchId equals batch.Id
                join warehouse in db.Warehouses on balance.WarehouseId equals warehouse.Id
                join branch in db.Branches on warehouse.BranchId equals branch.Id
                where variants.Contains(balance.VariantId) && warehouse.Active && branch.Active && (batch.ExpiresAt == null || batch.ExpiresAt > now)
                group balance by balance.VariantId into stock
                select new { VariantId = stock.Key, Available = stock.Sum(row => row.OnHand - row.Reserved) }
            ).ToDictionaryAsync(row => row.VariantId, row => row.Available, ct);
            var states = await db.LowStock.Where(state => variants.Contains(state.VariantId)).ToDictionaryAsync(state => state.VariantId, ct);
            foreach (var item in items)
            {
                var available = balances.GetValueOrDefault(item.Id);
                if (!states.TryGetValue(item.Id, out var state))
                {
                    state = new LowStockState { TenantId = group.Key, VariantId = item.Id };
                    db.LowStock.Add(state);
                }
                var low = available <= item.ReorderLevel;
                if (low && (!state.Low || state.NotifiedAt is null || state.NotifiedAt <= now.AddHours(-24)))
                {
                    if (!state.Low) state.Episode++;
                    db.Emit(tenantScope, ServiceManifest.Get("Inventory"), services.GetRequiredService<IHttpContextAccessor>(),
                        "inventory.low-stock.v1", new StockAlertEvent(item.Id, item.Sku, available, item.ReorderLevel, state.Episode, now));
                    state.NotifiedAt = now;
                }
                state.Low = low; state.Available = available; state.ObservedAt = now;
            }
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
