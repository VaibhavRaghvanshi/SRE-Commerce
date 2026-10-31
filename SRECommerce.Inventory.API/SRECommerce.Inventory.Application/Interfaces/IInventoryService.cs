namespace SRECommerce.Inventory.Application;
public interface IInventoryService
{
    Task<object> Lookups(Guid tenant,CancellationToken ct);
    Task<object> Branch(Guid tenant,Guid? id,BranchRequest request,CancellationToken ct);
    Task<object> Warehouse(Guid tenant,Guid? id,WarehouseRequest request,CancellationToken ct);
    Task<object> Register(Guid tenant,RegisterItemRequest request,CancellationToken ct);
    Task<object> Supplier(Guid tenant,Guid? id,SupplierRequest request,CancellationToken ct);
    Task<BalanceView[]> Balances(Guid tenant,Guid? warehouse,Guid? variant,CancellationToken ct);
    Task<object> Movements(Guid tenant,Guid? balance,int page,CancellationToken ct);
    Task<AvailabilityView[]> Availability(Guid tenant,Guid[] variants,CancellationToken ct);
    Task<BalanceView> Change(Guid tenant,StockChangeRequest request,CancellationToken ct);
    Task<ReservationView> Reserve(Guid tenant,ReserveRequest request,bool machine,CancellationToken ct);
    Task<ReservationView> Close(Guid tenant,Guid id,bool commit,CloseReservationRequest request,bool machine,CancellationToken ct);
    Task<ReservationView> Reservation(Guid tenant,Guid id,CancellationToken ct);
    Task<int> Expire(Guid tenant,CancellationToken ct);
    Task<PurchaseView> Purchase(Guid tenant,PurchaseRequest request,CancellationToken ct);
    Task<PurchaseView[]> Purchases(Guid tenant,int page,CancellationToken ct);
    Task<PurchaseView> ChangePurchaseStatus(Guid tenant,Guid id,bool cancel,string version,CancellationToken ct);
    Task<PurchaseView> Receive(Guid tenant,Guid id,PurchaseReceiveRequest request,CancellationToken ct);
}
