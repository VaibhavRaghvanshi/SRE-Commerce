namespace SRECommerce.Inventory.Application;
public interface IInventoryService
{
 Task<object> WarehouseForService(Guid tenant,Guid id,CancellationToken ct);
 Task<object[]> WarehousesForService(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<Guid[]> ImportOpeningStock(Guid tenant,OpeningStockImport request,CancellationToken ct);
    Task<object> Lookups(Guid tenant,CancellationToken ct);
    Task<object> Branch(Guid tenant,Guid? id,BranchRequest request,CancellationToken ct);
    Task<object> Warehouse(Guid tenant,Guid? id,WarehouseRequest request,CancellationToken ct);
    Task<object> Register(Guid tenant,RegisterItemRequest request,CancellationToken ct);
    Task<object> Supplier(Guid tenant,Guid? id,SupplierRequest request,CancellationToken ct);
    Task<BalanceView[]> Balances(Guid tenant,Guid? warehouse,Guid? variant,CancellationToken ct);
    Task<object> Movements(Guid tenant,Guid? balance,int page,CancellationToken ct);
    Task<AvailabilityView[]> Availability(Guid tenant,Guid[] variants,CancellationToken ct);
    Task<AvailabilityView[]> ShoppingAvailability(Guid tenant,Guid[] variants,CancellationToken ct);
    Task<BalanceView> Change(Guid tenant,StockChangeRequest request,CancellationToken ct);
    Task<ReservationView> Reserve(Guid tenant,ReserveRequest request,bool machine,CancellationToken ct);
    Task<ReservationView> Close(Guid tenant,Guid id,bool commit,CloseReservationRequest request,bool machine,CancellationToken ct);
    Task<ReservationView> Reservation(Guid tenant,Guid id,CancellationToken ct);
    Task<ReservationView> RecoveryReservation(Guid tenant,Guid id,CancellationToken ct);
    Task<WarehouseAvailability[]> AllocationOptions(Guid tenant,Guid[] variants,DateTimeOffset expiresAt,CancellationToken ct);
    Task<object> Abort(Guid tenant,Guid reservation,ReservationAbortRequest request,CancellationToken ct);
    Task<BalanceView[]> CustomerReturn(Guid tenant,CustomerReturnReceipt request,CancellationToken ct);
    Task<BalanceView> ReceiveProduction(Guid tenant,ManufacturingReceipt request,CancellationToken ct);
    Task<int> Expire(Guid tenant,CancellationToken ct);
    Task<PurchaseView> Purchase(Guid tenant,PurchaseRequest request,CancellationToken ct);
    Task<PurchaseView[]> Purchases(Guid tenant,int page,CancellationToken ct);
    Task<PurchaseView> ChangePurchaseStatus(Guid tenant,Guid id,bool cancel,string version,CancellationToken ct);
    Task<PurchaseView> Receive(Guid tenant,Guid id,PurchaseReceiveRequest request,CancellationToken ct);
}
