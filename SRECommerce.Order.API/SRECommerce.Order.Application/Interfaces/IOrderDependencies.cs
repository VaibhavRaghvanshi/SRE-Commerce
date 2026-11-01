namespace SRECommerce.Order.Application;
public interface IOrderDependencies
{
 Task AbortCart(Guid tenant,Guid order,Guid preparation,Guid? user,string? role,CancellationToken ct);Task AbortPrice(Guid tenant,Guid order,Guid quote,Guid? user,CancellationToken ct);Task AbortStock(Guid tenant,Guid reservation,Guid operation,CancellationToken ct);Task<RemotePayment> AbortPayment(Guid tenant,Guid order,CancellationToken ct);
 Task<SnapshotCartClaim> ClaimCart(Guid tenant,Guid order,Guid preparation,Guid? user,string? role,string? cartCapability,CancellationToken ct);
 Task<SnapshotCartClaim?> CartClaim(Guid tenant,Guid order,CancellationToken ct);Task<SnapshotCartClaim> CloseCart(Guid tenant,Guid order,bool complete,string version,CancellationToken ct);
 Task<SnapshotProduct[]> Products(Guid tenant,Guid[] variants,CancellationToken ct);Task<ShippingQuoteSnapshot> ShippingQuote(Guid tenant,Guid quote,CancellationToken ct);
 Task<RemoteRedemption> ClaimPrice(Guid tenant,Guid order,Guid quote,Guid? user,CancellationToken ct);Task<RemoteRedemption?> Redemption(Guid tenant,Guid order,CancellationToken ct);Task<RemoteRedemption> ClosePrice(Guid tenant,Guid order,bool redeem,string version,CancellationToken ct);
 Task<RemoteWarehouse[]> Warehouses(Guid tenant,Guid[] variants,DateTimeOffset expiry,CancellationToken ct);Task<RemoteReservation> Reserve(Guid tenant,Guid id,Guid warehouse,string reference,DateTimeOffset expiry,OrderQuantity[] items,CancellationToken ct);Task<RemoteReservation?> Reservation(Guid tenant,Guid id,CancellationToken ct);Task<RemoteReservation> CloseStock(Guid tenant,Guid id,bool commit,string version,CancellationToken ct);
 Task<RemotePayment> StartPayment(Guid tenant,Guid order,CancellationToken ct);Task<RemotePayment?> Payment(Guid tenant,Guid order,CancellationToken ct);Task<RemotePayment> CapturePayment(Guid tenant,Guid order,string version,CancellationToken ct);Task Refund(Guid tenant,Guid order,Guid operation,decimal amount,string reason,CancellationToken ct);
 Task CreateShipment(Guid tenant,Guid fulfillment,CancellationToken ct);Task ReceiveReturn(Guid tenant,Guid operation,Guid order,Guid warehouse,OrderQuantity[] items,bool resalable,CancellationToken ct);
}
