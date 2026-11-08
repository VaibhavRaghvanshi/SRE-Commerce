using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public interface IOrderService
{
 Task<object> Policy(Guid tenant,OrderPolicyRequest? request,bool publicRead,CancellationToken ct);
 Task<CheckoutView> Checkout(Guid tenant,CheckoutRequest request,bool guest,string? cartCapability,string? orderCapability,CancellationToken ct);
 Task<CheckoutView> CheckoutStatus(Guid tenant,Guid id,bool guest,string? capability,CancellationToken ct);
 Task<OrderView> Order(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct);Task<OrderPage> Orders(Guid tenant,int page,OrderStatus? status,bool admin,CancellationToken ct);
 Task<OrderView> Cancel(Guid tenant,Guid id,CancelOrderRequest request,bool guest,string? capability,bool admin,CancellationToken ct);Task<OrderView> ChangeStatus(Guid tenant,Guid id,OrderStateRequest request,CancellationToken ct);Task<object> History(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct);
 Task<FulfillmentView> Fulfill(Guid tenant,Guid id,FulfillOrderRequest request,CancellationToken ct);Task<FulfillmentView[]> Fulfillments(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct);Task<FulfillmentView> ChangeFulfillment(Guid tenant,Guid id,FulfillmentStateRequest request,bool machine,CancellationToken ct);
 Task<ReturnView> RequestReturn(Guid tenant,Guid id,ReturnOrderRequest request,bool guest,string? capability,CancellationToken ct);Task<ReturnView[]> Returns(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct);Task<ReturnView> ChangeReturn(Guid tenant,Guid id,ReturnStateRequest request,CancellationToken ct);
 Task<ExchangeView> RequestExchange(Guid tenant,Guid id,ExchangeOrderRequest request,bool guest,string? capability,CancellationToken ct);Task<ExchangeView[]> Exchanges(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct);Task<ExchangeView> ChangeExchange(Guid tenant,Guid id,ExchangeStateRequest request,CancellationToken ct);
 Task<OrderView> Invoice(Guid tenant,Guid id,InvoiceRequest request,CancellationToken ct);Task<Guid[]> ImportHistory(Guid tenant,HistoricalOrderImport request,CancellationToken ct);
 Task<GstWorksheet> PreviewGstWorksheet(Guid tenant,Guid id,GstWorksheetRequest request,CancellationToken ct);
 Task<OrderView> CounterHandover(Guid tenant,Guid id,CounterHandoverRequest request,CancellationToken ct);
 Task<MachineOrderContext> Context(Guid tenant,Guid id,CancellationToken ct);Task<object> ShipmentContext(Guid tenant,Guid id,CancellationToken ct);Task<object> ReturnContext(Guid tenant,Guid id,CancellationToken ct);
 Task<object> NotificationContext(Guid tenant,Guid id,Guid eventId,int kind,CancellationToken ct);
 Task<bool> VerifiedPurchase(Guid tenant,Guid user,Guid product,CancellationToken ct);
 Task<CheckoutReview[]> Reviews(Guid tenant,int page,CancellationToken ct);
 Task<CheckoutView> RecoverCheckout(Guid tenant,Guid id,CheckoutRecoveryRequest request,CancellationToken ct);
 Task<CheckoutView> CancelCheckout(Guid tenant,Guid id,CancelCheckoutRequest request,bool guest,string? capability,CancellationToken ct);
}
