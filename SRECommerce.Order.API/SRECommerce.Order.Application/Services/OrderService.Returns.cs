using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed partial class OrderService
{
 private static decimal Round(decimal amount,int places)=>decimal.Round(amount,places,MidpointRounding.AwayFromZero);
 private async Task<OrderReturn> ReturnCore(Guid tenant,SalesOrder order,Guid id,OrderQuantity[] quantities,string reason,CancellationToken ct)
 {
  var policy=await Config(ct);if(order.Historical||policy.ReturnWindowDays==0||order.DeliveredAt is not {} delivered||delivered.AddDays(policy.ReturnWindowDays)<Now||order.Status is not(OrderStatus.PartiallyFulfilled or OrderStatus.Fulfilled or OrderStatus.Delivered or OrderStatus.Completed))throw new BusinessFault("OrderReturnUnavailable");
  Quantities(quantities);var lines=new List<ReturnLine>();foreach(var item in quantities)
  {var line=order.Items.SingleOrDefault(i=>i.VariantId==item.VariantId)??throw new BusinessFault("OrderItemNotFound",404);if(line.ReturnRequested+item.Quantity>line.Delivered)throw new BusinessFault("ReturnQuantityExceeded");var target=Round(line.Total*(line.ReturnRequested+item.Quantity)/line.Quantity,order.DecimalPlaces);var amount=target-line.RefundAllocated;if(amount<0||target>line.Total)throw new BusinessFault("ReturnRefundLimitExceeded");line.ReturnRequested+=item.Quantity;line.RefundAllocated=target;lines.Add(new(item.VariantId,item.Quantity,amount));}
  var result=new OrderReturn{TenantId=tenant,Id=id,OrderId=order.Id,Items=ContractJson.Write(lines),Reason=OrderRules.Text(reason,300),RefundAmount=lines.Sum(i=>i.RefundAmount),CreatedAt=Now};repository.Add(result);return result;
 }
 public async Task<ReturnView> RequestReturn(Guid tenant,Guid id,ReturnOrderRequest request,bool guest,string? capability,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,ActorPermission.Customer,ct);Id(request.Id);var hash=Hash(request);
  return await repository.Transaction(async token=>{var order=await Owned(id,guest,capability,false,token);var previous=await ReplayResource<ReturnView>(request.Id,id,"order.return.requested",hash,token);if(previous is not null)return previous;repository.Version(order,request.Version);var value=await ReturnCore(tenant,order,request.Id,request.Items,request.Reason,token);return await RecordResource(order,request.Id,"order.return.requested",hash,()=>View(value),token);},ct);
 }
 public async Task<ReturnView[]> Returns(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct)
 {await access.Authorize(tenant,guest,admin?ActorPermission.Staff:ActorPermission.Customer,ct);await Owned(id,guest,capability,admin,ct);return(await repository.Returns(id,ct)).Select(View).ToArray();}
 public async Task<ReturnView> ChangeReturn(Guid tenant,Guid id,ReturnStateRequest request,CancellationToken ct)
 {
  await access.Authorize(tenant,false,ActorPermission.Manager,ct);Id(request.OperationId);var hash=Hash(request);
  return await repository.Transaction(async token=>
  {
   var value=await repository.Return(id,token)??throw new BusinessFault("ReturnNotFound",404);var order=await repository.Order(value.OrderId,token)??throw new BusinessFault("OrderNotFound",404);var previous=await ReplayResource<ReturnView>(request.OperationId,order.Id,"order.return.status",hash,token);if(previous is not null)return previous;repository.Version(value,request.Version);
   if(value.Status==ReturnStatus.Requested&&request.Status is ReturnStatus.Approved or ReturnStatus.Rejected or ReturnStatus.Cancelled)value.Status=request.Status;
   else if(value.Status==ReturnStatus.Approved&&request.Status==ReturnStatus.Received){if(request.WarehouseId is not {} warehouse||warehouse==Guid.Empty)throw new BusinessFault("ReturnWarehouseRequired",400);value.WarehouseId=warehouse;value.Resalable=request.Resalable;value.Status=ReturnStatus.Receiving;}
   else throw new BusinessFault("InvalidReturnTransition");
   if(value.Status is ReturnStatus.Rejected or ReturnStatus.Cancelled)foreach(var item in ContractJson.Read<ReturnLine[]>(value.Items)){var line=order.Items.Single(i=>i.VariantId==item.VariantId);line.ReturnRequested-=item.Quantity;line.RefundAllocated-=item.RefundAmount;}
   if(value.ExchangeId is {} exchangeId&&await repository.Exchange(exchangeId,token) is {} exchange){if(value.Status==ReturnStatus.Approved)exchange.Status=ExchangeStatus.Approved;else if(value.Status is ReturnStatus.Rejected or ReturnStatus.Cancelled)exchange.Status=value.Status==ReturnStatus.Rejected?ExchangeStatus.Rejected:ExchangeStatus.Cancelled;}
   return await RecordResource(order,request.OperationId,"order.return.status",hash,()=>View(value),token);
  },ct);
 }
 public async Task<ExchangeView> RequestExchange(Guid tenant,Guid id,ExchangeOrderRequest request,bool guest,string? capability,CancellationToken ct)
 {
  await access.Authorize(tenant,guest,ActorPermission.Customer,ct);Id(request.Id);Id(request.ReplacementVariantId);var policy=await Config(ct);if(!policy.AllowExchanges)throw new BusinessFault("ExchangesDisabled",403);var hash=Hash(request);
  return await repository.Transaction(async token=>
  {
   var order=await Owned(id,guest,capability,false,token);var previous=await ReplayResource<ExchangeView>(request.Id,id,"order.exchange.requested",hash,token);if(previous is not null)return previous;repository.Version(order,request.Version);
   var products=await dependencies.Products(tenant,[request.ReplacementVariantId],token);var product=products.SingleOrDefault(p=>p.Variants.Any(v=>v.Id==request.ReplacementVariantId&&v.Active))??throw new BusinessFault("ReplacementUnavailable");if(product.B2BOnly&&(guest||access.Actor?.TenantRole is not("Owner" or "Manager" or "B2BBuyer")))throw new BusinessFault("ReplacementUnavailable");
   var returnId=OrderRules.Operation(request.Id,"exchange-return");var returned=await ReturnCore(tenant,order,returnId,[new(request.OriginalVariantId,request.Quantity)],request.Reason,token);returned.ExchangeId=request.Id;
   var value=new OrderExchange{TenantId=tenant,Id=request.Id,OrderId=id,ReturnId=returnId,OriginalVariantId=request.OriginalVariantId,ReplacementVariantId=request.ReplacementVariantId,Quantity=request.Quantity,CreatedAt=Now};repository.Add(value);return await RecordResource(order,request.Id,"order.exchange.requested",hash,()=>View(value),token);
  },ct);
 }
 public async Task<ExchangeView[]> Exchanges(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct)
 {await access.Authorize(tenant,guest,admin?ActorPermission.Staff:ActorPermission.Customer,ct);await Owned(id,guest,capability,admin,ct);return(await repository.Exchanges(id,ct)).Select(View).ToArray();}
 public async Task<ExchangeView> ChangeExchange(Guid tenant,Guid id,ExchangeStateRequest request,CancellationToken ct)
 {
  await access.Authorize(tenant,false,ActorPermission.Manager,ct);Id(request.OperationId);var hash=Hash(request);
  return await repository.Transaction(async token=>
  {
   var value=await repository.Exchange(id,token)??throw new BusinessFault("ExchangeNotFound",404);var order=await repository.Order(value.OrderId,token)??throw new BusinessFault("OrderNotFound",404);var previous=await ReplayResource<ExchangeView>(request.OperationId,order.Id,"order.exchange.status",hash,token);if(previous is not null)return previous;repository.Version(value,request.Version);var returned=await repository.Return(value.ReturnId,token)??throw new BusinessFault("ReturnNotFound",404);
   if(request.Status==ExchangeStatus.ReplacementPending&&value.Status==ExchangeStatus.ReturnReceived&&request.ReplacementOrderId is {} replacementId)
   {
    var replacement=await repository.Order(replacementId,token)??throw new BusinessFault("ReplacementOrderNotFound",404);if(replacement.Id==order.Id||replacement.Historical||replacement.UserId!=order.UserId||!replacement.Items.Any(i=>i.VariantId==value.ReplacementVariantId&&i.Quantity>=value.Quantity)||replacement.Status is not(OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.PartiallyFulfilled or OrderStatus.Fulfilled or OrderStatus.Delivered or OrderStatus.Completed))throw new BusinessFault("ReplacementOrderNotEligible");
    if(order.UserId is null&&!string.Equals(ContractJson.Read<OrderContact>(order.ContactDocument).Email,ContractJson.Read<OrderContact>(replacement.ContactDocument).Email,StringComparison.OrdinalIgnoreCase))throw new BusinessFault("ReplacementCustomerMismatch");value.ReplacementOrderId=replacementId;value.Status=request.Status;
   }
   else if(request.Status==ExchangeStatus.Completed&&value.Status==ExchangeStatus.ReplacementPending&&value.ReplacementOrderId is {} replacementId2)
   {var replacement=await repository.Order(replacementId2,token)??throw new BusinessFault("ReplacementOrderNotFound",404);if(replacement.Status is not(OrderStatus.Delivered or OrderStatus.Completed)||returned.Status!=ReturnStatus.Refunded)throw new BusinessFault("ExchangeSettlementIncomplete");value.Status=ExchangeStatus.Completed;}
   else throw new BusinessFault("UseReturnApprovalWorkflow");
   return await RecordResource(order,request.OperationId,"order.exchange.status",hash,()=>View(value),token);
  },ct);
 }
 public async Task<object> ReturnContext(Guid tenant,Guid id,CancellationToken ct)
 {access.Recovery(tenant,["Payment","Shipping"]);var returned=await repository.Return(id,ct)??throw new BusinessFault("ReturnNotFound",404);var order=await repository.Order(returned.OrderId,ct)??throw new BusinessFault("OrderNotFound",404);return new{Return=View(returned),Order=View(order)};}
}
