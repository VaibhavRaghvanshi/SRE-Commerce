using System.Security.Cryptography;using System.Text;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed partial class OrderService(IOrderRepository repository,IOrderAccess access,IOrderDependencies dependencies,IOrderSaga saga,TimeProvider clock):IOrderService
{
 private DateTimeOffset Now=>clock.GetUtcNow();private static string Version(byte[] version)=>Convert.ToBase64String(version);
 private static void Id(Guid id){if(id==Guid.Empty)throw new BusinessFault("InvalidOrderId",400);}
 private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(value))));
 private static OrderContact Contact(OrderContact value)
 {
  OrderRules.Text(value.FullName);OrderRules.Text(value.AddressLine1,200);if(!string.IsNullOrWhiteSpace(value.AddressLine2))OrderRules.Text(value.AddressLine2,200);OrderRules.Text(value.City,100);
  if(value.Email.Length>254||!System.Net.Mail.MailAddress.TryCreate(value.Email,out var email)||email.Address!=value.Email)throw new BusinessFault("InvalidContactEmail",400);
  if(value.Phone is not null&&(value.Phone.Length is <7 or >25||value.Phone.Any(c=>!char.IsAsciiDigit(c)&&c!='+'&&c!='-'&&c!=' ')))throw new BusinessFault("InvalidContactPhone",400);
  if(value.CountryCode.Length!=2||value.CountryCode.Any(c=>c is <'A' or >'Z')||value.PostalCode.Length is <2 or >16||value.PostalCode.Any(c=>!char.IsAsciiLetterOrDigit(c)&&c!=' '&&c!='-'))throw new BusinessFault("InvalidShippingRegion",400);
  return value with{FullName=value.FullName.Trim(),Email=email.Address,AddressLine1=value.AddressLine1.Trim(),AddressLine2=value.AddressLine2?.Trim(),City=value.City.Trim(),PostalCode=value.PostalCode.Trim().ToUpperInvariant()};
 }
 internal static OrderView View(SalesOrder o)=>new(o.Id,o.Number,o.Status,o.PaymentState,o.PaymentMethod,o.Currency,o.DecimalPlaces,o.Subtotal,o.Discount,o.Net,o.Tax,o.ItemsTotal,o.ShippingTotal,o.Total,ContractJson.Read<OrderContact>(o.ContactDocument),o.Items.OrderBy(i=>i.Sku).Select(i=>new OrderLineView(i.ProductId,i.VariantId,i.Sku,i.Name,i.Quantity,i.Fulfilled,i.Delivered,i.ReturnRequested,i.Returned,i.UnitPrice,i.Subtotal,i.Discount,i.Net,i.Tax,i.Total,i.ShipmentRequested,i.RefundAllocated)).ToArray(),o.InvoiceReference,o.Historical,o.CreatedAt,Version(o.Version),o.Refunded,o.DeliveredAt);
 private async Task<CheckoutView> CheckoutView(CheckoutIntent intent,CancellationToken ct)=>new(intent.Id,intent.State,intent.Phase,intent.State==Domain.CheckoutState.ManualReview?"CheckoutNeedsSupport":intent.State==Domain.CheckoutState.Cancelled?"CheckoutCancelled":intent.State==Domain.CheckoutState.Failed?"CheckoutFailed":null,intent.CreatedAt,await repository.Order(intent.Id,ct) is {} order?View(order):null);
 private void Owner(CheckoutIntent intent,bool guest,string? capability)
 {
  if(guest){if(intent.UserId is not null||intent.GuestKeyHash is null||!CryptographicOperations.FixedTimeEquals(intent.GuestKeyHash,OrderRules.Capability(capability)))throw new BusinessFault("CheckoutNotFound",404);}
  else if(intent.UserId!=access.Actor?.UserId)throw new BusinessFault("CheckoutNotFound",404);
 }
 private async Task<SalesOrder> Owned(Guid id,bool guest,string? capability,bool admin,CancellationToken ct)
 {
  var order=await repository.Order(id,ct)??throw new BusinessFault("OrderNotFound",404);if(admin)return order;
  if(guest){var intent=await repository.Intent(id,ct)??throw new BusinessFault("OrderNotFound",404);Owner(intent,true,capability);}
  else if(order.UserId!=access.Actor?.UserId)throw new BusinessFault("OrderNotFound",404);return order;
 }

 private async Task<OrderPolicy> Config(CancellationToken ct)=>await repository.Policy(ct)??throw new BusinessFault("OrderingNotConfigured");
 public async Task<object> Policy(Guid tenant,OrderPolicyRequest? request,bool publicRead,CancellationToken ct)
 {
  await access.Authorize(tenant,publicRead,publicRead?ActorPermission.Customer:ActorPermission.Owner,ct);
  if(request is null){var value=await Config(ct);return new{value.AllowGuest,value.AllowCOD,value.AllowExchanges,value.ReturnWindowDays,value.MaximumTotal,Version=publicRead?null:Version(value.Version)};}
  if(publicRead)throw new BusinessFault("OwnerPolicyRequired",403);if(request.ReturnWindowDays is <0 or >365||request.MaximumTotal<=0)throw new BusinessFault("InvalidOrderPolicy",400);OrderRules.Money(request.MaximumTotal);
  return await repository.Transaction(async token=>{var value=await repository.Policy(token);if(value is null){value=new OrderPolicy{TenantId=tenant,Id=tenant};repository.Add(value);}else repository.Version(value,request.Version??"");value.AllowGuest=request.AllowGuest;value.AllowCOD=request.AllowCOD;value.AllowExchanges=request.AllowExchanges;value.ReturnWindowDays=request.ReturnWindowDays;value.MaximumTotal=request.MaximumTotal;await repository.Commit("order.policy.saved",tenant,null,token);return new{value.AllowGuest,value.AllowCOD,value.AllowExchanges,value.ReturnWindowDays,value.MaximumTotal,Version=Version(value.Version)};},ct);
 }
 public async Task<CheckoutView> Checkout(Guid tenant,CheckoutRequest request,bool guest,string? cartCapability,string? orderCapability,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,ActorPermission.Customer,ct);await access.RequireOrdering(tenant,ct);Id(request.Id);Id(request.PreparationId);Id(request.ShippingQuoteId);OrderRules.Payment(request.PaymentMethod);
  var policy=await Config(ct);if(guest&&!policy.AllowGuest||request.PaymentMethod=="COD"&&!policy.AllowCOD)throw new BusinessFault("CheckoutPolicyDenied",403);
  request=request with{Contact=Contact(request.Contact)};var user=guest?null:access.Actor?.UserId;var role=guest?null:access.Actor is {IsPlatformAdmin:true}?"PlatformAdmin":access.Actor?.TenantRole;var digest=guest?OrderRules.Capability(orderCapability):null;
  if(guest)OrderRules.Capability(cartCapability);var hash=Hash(new{request,UserId=user,Role=role});
  await repository.Transaction(async token=>
  {
   var previous=await repository.Intent(request.Id,token);if(previous is not null){Owner(previous,guest,orderCapability);if(previous.RequestHash!=hash)throw new BusinessFault("CheckoutKeyReused");return true;}
   repository.Add(new CheckoutIntent{TenantId=tenant,Id=request.Id,PreparationId=request.PreparationId,ShippingQuoteId=request.ShippingQuoteId,UserId=user,CustomerRole=role,GuestKeyHash=digest,RequestHash=hash,RequestDocument=ContractJson.Write(request),CreatedAt=Now,Deadline=Now.AddMinutes(5),NextAttemptAt=Now});await repository.Commit("order.checkout.intent",request.Id,null,token);return true;
  },ct);
  // The initial inline step proves the guest/customer cart. Recovery never stores or replays its secret.
  await saga.Run(tenant,request.Id,cartCapability,ct,true);return await CheckoutView(await repository.Intent(request.Id,ct)??throw new BusinessFault("CheckoutNotFound",404),ct);
 }
 public async Task<CheckoutView> CheckoutStatus(Guid tenant,Guid id,bool guest,string? capability,CancellationToken ct)
 {await access.Authorize(tenant,guest,ActorPermission.Customer,ct);var intent=await repository.Intent(id,ct)??throw new BusinessFault("CheckoutNotFound",404);Owner(intent,guest,capability);return await CheckoutView(intent,ct);}
 public async Task<OrderView> Order(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct)
 {await access.Authorize(tenant,guest,admin?ActorPermission.Staff:ActorPermission.Customer,ct);return View(await Owned(id,guest,capability,admin,ct));}
 public async Task<OrderPage> Orders(Guid tenant,int page,OrderStatus? status,bool admin,CancellationToken ct)
 {
  await access.Authorize(tenant,false,admin?ActorPermission.Staff:ActorPermission.Customer,ct);if(page is <1 or >10000||status is {} requested&&!Enum.IsDefined(requested))throw new BusinessFault("InvalidOrderQuery",400);
  var result=await repository.Orders(page,status,admin?null:access.Actor!.UserId,ct);return new(result.Items.Select(View).ToArray(),result.Total,page,50);
 }
 public async Task<object> History(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct)
 {await access.Authorize(tenant,guest,admin?ActorPermission.Staff:ActorPermission.Customer,ct);await Owned(id,guest,capability,admin,ct);return await repository.History(id,ct);}
 private async Task<OrderView?> Replay(Guid operation,Guid order,string action,string hash,CancellationToken ct)
 {Id(operation);var prior=await repository.Operation(operation,ct);if(prior is null)return null;if(prior.OrderId!=order||prior.Action!=action||prior.Hash!=hash)throw new BusinessFault("OrderOperationKeyReused");return ContractJson.Read<OrderView>(prior.Result);}
 private async Task<OrderView> Record(SalesOrder order,Guid operation,string action,string hash,CancellationToken ct)
 {
  order.Revision++;order.UpdatedAt=Now;await repository.Commit(action,order.Id,new{OrderId=order.Id,order.Status},ct);var result=View(order);repository.Add(new OrderOperation{TenantId=order.TenantId,Id=operation,OrderId=order.Id,Action=action,Hash=hash,Result=ContractJson.Write(result),CreatedAt=Now});await repository.Commit("order.operation.recorded",operation,null,ct);return result;
 }
 private void Transition(SalesOrder order,OrderStatus status,string reason)
 {var previous=order.Status;OrderRules.Move(order,status);repository.Add(new OrderStatusHistory{TenantId=order.TenantId,OrderId=order.Id,Previous=previous,Current=status,Reason=OrderRules.Text(reason,300),CreatedAt=Now});}
 public async Task<OrderView> Cancel(Guid tenant,Guid id,CancelOrderRequest request,bool guest,string? capability,bool admin,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,admin?ActorPermission.Manager:ActorPermission.Customer,ct);var hash=Hash(new{Action="cancel",request});OrderRules.Text(request.Reason,300);
  return await repository.Transaction(async token=>
  {
   var order=await Owned(id,guest,capability,admin,token);var prior=await Replay(request.OperationId,id,"order.cancel.requested",hash,token);if(prior is not null)return prior;repository.Version(order,request.Version);
   if(order.Historical||order.Items.Any(i=>i.ShipmentRequested>0)||order.Status is OrderStatus.Completed or OrderStatus.Delivered or OrderStatus.Cancelled or OrderStatus.Failed)throw new BusinessFault("OrderCannotBeCancelled");
   var intent=await repository.Intent(id,token)??throw new BusinessFault("CheckoutNotFound",404);intent.State=Domain.CheckoutState.Compensating;intent.Phase=SagaPhase.CompensatePayment;intent.CancellationReason=request.Reason;intent.LeaseId=null;intent.LeaseUntil=null;intent.NextAttemptAt=Now;intent.Attempts=0;Transition(order,OrderStatus.CancellationPending,request.Reason);
   return await Record(order,request.OperationId,"order.cancel.requested",hash,token);
  },ct);
 }
 public async Task<OrderView> ChangeStatus(Guid tenant,Guid id,OrderStateRequest request,CancellationToken ct)
 {
  await access.Authorize(tenant,false,ActorPermission.Manager,ct);var hash=Hash(request);
  return await repository.Transaction(async token=>
  {
   var order=await Owned(id,false,null,true,token);var prior=await Replay(request.OperationId,id,"order.status.changed",hash,token);if(prior is not null)return prior;repository.Version(order,request.Version);
   if(order.Historical||request.Status is not(OrderStatus.Processing or OrderStatus.Completed))throw new BusinessFault("UseAuthoritativeOrderWorkflow");Transition(order,request.Status,request.Reason);return await Record(order,request.OperationId,"order.status.changed",hash,token);
  },ct);
 }
 public async Task<OrderView> Invoice(Guid tenant,Guid id,InvoiceRequest request,CancellationToken ct)
 {
  await access.Authorize(tenant,false,ActorPermission.Manager,ct);var reference=OrderRules.Text(request.Reference,80);var hash=Hash(request);
  return await repository.Transaction(async token=>
  {var order=await Owned(id,false,null,true,token);var prior=await Replay(request.OperationId,id,"order.invoice.referenced",hash,token);if(prior is not null)return prior;repository.Version(order,request.Version);if(order.InvoiceReference is not null&&order.InvoiceReference!=reference)throw new BusinessFault("InvoiceReferenceImmutable");order.InvoiceReference=reference;return await Record(order,request.OperationId,"order.invoice.referenced",hash,token);},ct);
 }
 public async Task<MachineOrderContext> Context(Guid tenant,Guid id,CancellationToken ct)
 {access.Recovery(tenant,["Payment","Shipping","Notification","Discovery"]);var value=await repository.Order(id,ct)??throw new BusinessFault("OrderNotFound",404);return new(value.Id,tenant,value.UserId,value.Currency,value.Total,value.PaymentMethod,value.Status,value.PaymentState,value.Historical,View(value).Items);}
}
