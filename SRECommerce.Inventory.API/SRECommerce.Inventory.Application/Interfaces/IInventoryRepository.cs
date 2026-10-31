using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public interface IInventoryRepository
{
    Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct);
    Task<IReadOnlyCollection<Branch>> Branches(CancellationToken ct);
    Task<IReadOnlyCollection<Warehouse>> Warehouses(CancellationToken ct);
    Task<IReadOnlyCollection<StockItem>> Items(CancellationToken ct);
    Task<IReadOnlyCollection<Supplier>> Suppliers(CancellationToken ct);
    Task<IReadOnlyCollection<InventoryStock>> Stock(Guid? warehouse,IReadOnlyCollection<Guid>? variants,CancellationToken ct);
    Task<IReadOnlyCollection<InventoryMovement>> Movements(Guid? balance,int page,CancellationToken ct);
    Task<StockReservation?> Reservation(Guid id,CancellationToken ct);
    Task<IReadOnlyCollection<StockReservation>> Expired(DateTimeOffset now,int count,CancellationToken ct);
    Task<StockOperation?> Operation(Guid id,CancellationToken ct);
    Task<PurchaseOrder?> Purchase(Guid id,CancellationToken ct);
    Task<IReadOnlyCollection<PurchaseOrder>> Purchases(int page,CancellationToken ct);
    void Add<T>(T entity)where T:class;
    void Version(BuildingBlocks.Kernel.IVersionedRecord entity,string? expected);
    Task Commit(string action,Guid resource,object? integrationEvent,CancellationToken ct);
}
