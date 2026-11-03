using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Manufacturing.Domain;

namespace SRECommerce.Manufacturing.Application;

public interface IManufacturingAccess
{
    Guid? Actor { get; }
    Task Human(Guid tenant, ActorPermission role, CancellationToken ct);
    Task<string> Currency(Guid tenant, CancellationToken ct);
}
public interface IManufacturingInventory
{
    Task Validate(Guid tenant, Guid warehouse, Guid[] variants, CancellationToken ct);
    Task<InventoryReservation> Reserve(ProductionOrder order, ProductionMaterial[] materials, CancellationToken ct);
    Task<InventoryReservation> Consume(ProductionOrder order, CancellationToken ct);
    Task Cancel(ProductionOrder order, CancellationToken ct);
    Task Receive(ProductionOrder order, CancellationToken ct);
}
public interface IManufacturingStore
{
    Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<BillOfMaterials?> Bom(Guid id, CancellationToken ct);
    Task<BillOfMaterials[]> Boms(int page, CancellationToken ct);
    Task<int> Revision(string code, CancellationToken ct);
    Task<ProductionOrder?> Order(Guid id, CancellationToken ct);
    Task<ProductionOrder[]> Orders(int page, CancellationToken ct);
    Task<ProductionHistory[]> History(Guid id, CancellationToken ct);
    void Add<T>(T row) where T : class;
    void Version(ProductionOrder row, string expected);
    Task Save(string action, Guid resource, CancellationToken ct);
}
