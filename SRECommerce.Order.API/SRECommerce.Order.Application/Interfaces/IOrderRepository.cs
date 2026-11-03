using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public interface IOrderRepository
{
 Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct);
 Task<OrderFulfillment[]> PendingFulfillments(DateTimeOffset now,CancellationToken ct);Task<OrderReturn[]> PendingReturns(DateTimeOffset now,CancellationToken ct);Task<SalesOrder[]> PendingCOD(CancellationToken ct);
 Task<OrderPolicy?> Policy(CancellationToken ct);
 Task<CheckoutIntent[]> Due(DateTimeOffset now,CancellationToken ct);
 Task<CheckoutIntent?> FreshIntent(Guid id,CancellationToken ct);
 Task<CheckoutIntent[]> Reviews(int page,CancellationToken ct);
 Task<CheckoutIntent?> Intent(Guid id,CancellationToken ct);Task<SalesOrder?> Order(Guid id,CancellationToken ct);
 Task<(SalesOrder[] Items,int Total)> Orders(int page,OrderStatus? status,Guid? user,CancellationToken ct);
 Task<OrderReservation[]> Reservations(Guid order,CancellationToken ct);Task<OrderOperation?> Operation(Guid id,CancellationToken ct);Task<OrderStatusHistory[]> History(Guid order,CancellationToken ct);
 Task<OrderFulfillment?> Fulfillment(Guid id,CancellationToken ct);Task<OrderFulfillment[]> Fulfillments(Guid order,CancellationToken ct);
 Task<OrderReturn?> Return(Guid id,CancellationToken ct);Task<OrderReturn[]> Returns(Guid order,CancellationToken ct);Task<OrderExchange?> Exchange(Guid id,CancellationToken ct);Task<OrderExchange[]> Exchanges(Guid order,CancellationToken ct);
 Task<OrderImport?> Import(Guid id,CancellationToken ct);
 Task<Guid?> NotificationOrder(Guid eventId,string type,CancellationToken ct);
 Task<bool> VerifiedPurchase(Guid user,Guid product,CancellationToken ct);
 void EmitNotice(int kind,Guid order);
 void AuditCheckoutReview(Guid checkout,string reason);
 void Add<T>(T value)where T:class;void Version(SRECommerce.BuildingBlocks.Kernel.IVersionedRecord value,string expected);
 Task Commit(string action,Guid resource,object? integrationEvent,CancellationToken ct);
}
