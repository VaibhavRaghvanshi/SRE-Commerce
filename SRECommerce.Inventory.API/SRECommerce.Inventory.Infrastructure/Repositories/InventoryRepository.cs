using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Inventory.Application;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Infrastructure;
public sealed class InventoryRepository(InventoryDbContext db,ModuleScope scope,IHttpContextAccessor contexts,ServiceSpec service):IInventoryRepository
{
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var result=await action(ct);await transaction.CommitAsync(ct);return result;}
    public async Task<IReadOnlyCollection<Branch>> Branches(CancellationToken ct)=>await db.Branches.OrderBy(b=>b.Name).ToListAsync(ct);
    public async Task<IReadOnlyCollection<Warehouse>> Warehouses(CancellationToken ct)=>await db.Warehouses.OrderBy(w=>w.Code).ToListAsync(ct);
    public async Task<IReadOnlyCollection<StockItem>> Items(CancellationToken ct)=>await db.Items.OrderBy(i=>i.Sku).ToListAsync(ct);
    public async Task<IReadOnlyCollection<Supplier>> Suppliers(CancellationToken ct)=>await db.Suppliers.OrderBy(s=>s.Name).ToListAsync(ct);
    public async Task<IReadOnlyCollection<InventoryStock>> Stock(Guid? warehouse,IReadOnlyCollection<Guid>? variants,CancellationToken ct)
    {
        var balances=db.Balances.AsQueryable();if(warehouse is {} w)balances=balances.Where(b=>b.WarehouseId==w);if(variants is not null)balances=balances.Where(b=>variants.Contains(b.VariantId));
        return await(from balance in balances join batch in db.Batches on new{balance.TenantId,Id=balance.BatchId} equals new{batch.TenantId,batch.Id} orderby balance.Id select new InventoryStock(balance,batch)).ToListAsync(ct);
    }
    public async Task<IReadOnlyCollection<InventoryMovement>> Movements(Guid? balance,int page,CancellationToken ct)=>await db.Movements.AsNoTracking().Where(m=>balance==null||m.BalanceId==balance).OrderByDescending(m=>m.OccurredAt).ThenBy(m=>m.Id).Skip((page-1)*100).Take(100).ToListAsync(ct);
    public Task<StockReservation?> Reservation(Guid id,CancellationToken ct)=>db.Reservations.Include(r=>r.Lines).SingleOrDefaultAsync(r=>r.Id==id,ct);
    public async Task<IReadOnlyCollection<StockReservation>> Expired(DateTimeOffset now,int count,CancellationToken ct)=>await db.Reservations.Include(r=>r.Lines).Where(r=>r.Status==ReservationStatus.Active&&r.ExpiresAt<=now).OrderBy(r=>r.ExpiresAt).ThenBy(r=>r.Id).Take(count).ToListAsync(ct);
    public Task<StockOperation?> Operation(Guid id,CancellationToken ct)=>db.Operations.SingleOrDefaultAsync(o=>o.Id==id,ct);
    public Task<PurchaseOrder?> Purchase(Guid id,CancellationToken ct)=>db.Purchases.Include(p=>p.Lines).SingleOrDefaultAsync(p=>p.Id==id,ct);
    public async Task<IReadOnlyCollection<PurchaseOrder>> Purchases(int page,CancellationToken ct)=>await db.Purchases.Include(p=>p.Lines).OrderByDescending(p=>p.CreatedAt).ThenBy(p=>p.Id).Skip((page-1)*50).Take(50).ToListAsync(ct);
    public void Add<T>(T entity)where T:class=>db.Add(entity);
    public void Version(BuildingBlocks.Kernel.IVersionedRecord entity,string? expected)=>db.ExpectedVersion(entity,expected??"");
    public async Task Commit(string action,Guid resource,object? integrationEvent,CancellationToken ct)
    {db.Record(scope,contexts,action,resource.ToString());if(integrationEvent is not null)db.Emit(scope,service,contexts,action+".v1",integrationEvent);await db.SaveChangesAsync(ct);}
}
