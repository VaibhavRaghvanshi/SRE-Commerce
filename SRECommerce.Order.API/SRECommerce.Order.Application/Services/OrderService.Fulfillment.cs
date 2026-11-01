using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed partial class OrderService
{
 private static OrderQuantity[] Quantities(OrderQuantity[] items)
 {if(items is null||items.Length is <1 or >50||items.Select(i=>i.VariantId).Distinct().Count()!=items.Length)throw new BusinessFault("InvalidOrderQuantities",400);foreach(var item in items){Id(item.VariantId);OrderRules.Quantity(item.Quantity);}return items;}
 private static FulfillmentView View(OrderFulfillment f)=>new(f.Id,f.OrderId,f.WarehouseId,ContractJson.Read<OrderQuantity[]>(f.Items),f.Status,f.CourierReference,Version(f.Version));
 private static ReturnView View(OrderReturn r)=>new(r.Id,r.OrderId,ContractJson.Read<ReturnLine[]>(r.Items).Select(i=>new OrderQuantity(i.VariantId,i.Quantity)).ToArray(),r.Reason,r.Status,r.RefundAmount,r.WarehouseId,r.Resalable,r.ExchangeId,Version(r.Version));
 private static ExchangeView View(OrderExchange e)=>new(e.Id,e.OrderId,e.ReturnId,e.OriginalVariantId,e.ReplacementVariantId,e.Quantity,e.Status,e.ReplacementOrderId,Version(e.Version));
 private async Task<T?> ReplayResource<T>(Guid operation,Guid order,string action,string hash,CancellationToken ct)where T:class
 {Id(operation);var previous=await repository.Operation(operation,ct);if(previous is null)return null;if(previous.OrderId!=order||previous.Action!=action||previous.Hash!=hash)throw new BusinessFault("OrderOperationKeyReused");return ContractJson.Read<T>(previous.Result);}
 private async Task<T> RecordResource<T>(SalesOrder order,Guid operation,string action,string hash,Func<T> view,CancellationToken ct)
 {OrderRules.Lines(order);order.Revision++;order.UpdatedAt=Now;await repository.Commit(action,order.Id,new{OrderId=order.Id},ct);var result=view();repository.Add(new OrderOperation{TenantId=order.TenantId,Id=operation,OrderId=order.Id,Action=action,Hash=hash,Result=ContractJson.Write(result),CreatedAt=Now});await repository.Commit("order.operation.recorded",operation,null,ct);return result;}
 public async Task<FulfillmentView> Fulfill(Guid tenant,Guid id,FulfillOrderRequest request,CancellationToken ct)
 {
  await access.Authorize(tenant,false,ActorPermission.Manager,ct);Id(request.Id);Id(request.WarehouseId);Quantities(request.Items);var hash=Hash(request);
  return await repository.Transaction(async token=>
  {
   var order=await Owned(id,false,null,true,token);var previous=await ReplayResource<FulfillmentView>(request.Id,id,"order.fulfillment.requested",hash,token);if(previous is not null)return previous;repository.Version(order,request.Version);
   if(order.Historical||order.Status is not(OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.PartiallyFulfilled))throw new BusinessFault("OrderNotFulfillable");
   var allocations=(await repository.Reservations(id,token)).Where(r=>r.WarehouseId==request.WarehouseId&&r.Status==ReservationStep.Committed).SelectMany(r=>ContractJson.Read<OrderQuantity[]>(r.Items)).ToArray();
   var requested=(await repository.Fulfillments(id,token)).Where(f=>f.WarehouseId==request.WarehouseId&&f.Status is not(FulfillmentStatus.Cancelled or FulfillmentStatus.Failed)).SelectMany(f=>ContractJson.Read<OrderQuantity[]>(f.Items)).ToArray();
   foreach(var item in request.Items)
   {var line=order.Items.SingleOrDefault(i=>i.VariantId==item.VariantId)??throw new BusinessFault("OrderItemNotFound",404);var available=allocations.Where(a=>a.VariantId==item.VariantId).Sum(a=>a.Quantity)-requested.Where(a=>a.VariantId==item.VariantId).Sum(a=>a.Quantity);if(item.Quantity>available||line.ShipmentRequested+item.Quantity>line.Quantity)throw new BusinessFault("FulfillmentQuantityExceeded");line.ShipmentRequested+=item.Quantity;}
   var value=new OrderFulfillment{TenantId=tenant,Id=request.Id,OrderId=id,WarehouseId=request.WarehouseId,Items=ContractJson.Write(request.Items),CreatedAt=Now};repository.Add(value);return await RecordResource(order,request.Id,"order.fulfillment.requested",hash,()=>View(value),token);
  },ct);
 }
 public async Task<FulfillmentView[]> Fulfillments(Guid tenant,Guid id,bool guest,string? capability,bool admin,CancellationToken ct)
 {await access.Authorize(tenant,guest,admin?ActorPermission.Staff:ActorPermission.Customer,ct);await Owned(id,guest,capability,admin,ct);return(await repository.Fulfillments(id,ct)).Select(View).ToArray();}
 public async Task<FulfillmentView> ChangeFulfillment(Guid tenant,Guid id,FulfillmentStateRequest request,bool machine,CancellationToken ct)
 {
  if(!machine)throw new BusinessFault("ShippingWorkflowRequired",403);access.Recovery(tenant,["Shipping"]);Id(request.OperationId);var hash=Hash(request);
  return await repository.Transaction(async token=>
  {
   var fulfillment=await repository.Fulfillment(id,token)??throw new BusinessFault("FulfillmentNotFound",404);var order=await repository.Order(fulfillment.OrderId,token)??throw new BusinessFault("OrderNotFound",404);var previous=await ReplayResource<FulfillmentView>(request.OperationId,order.Id,"order.fulfillment.status",hash,token);if(previous is not null)return previous;repository.Version(fulfillment,request.Version);
   var allowed=fulfillment.Status switch{FulfillmentStatus.Requested=>request.Status is FulfillmentStatus.Created or FulfillmentStatus.Cancelled or FulfillmentStatus.Failed,FulfillmentStatus.Created=>request.Status is FulfillmentStatus.Shipped or FulfillmentStatus.Cancelled,FulfillmentStatus.Shipped=>request.Status==FulfillmentStatus.Delivered,_=>false};if(!allowed)throw new BusinessFault("InvalidFulfillmentTransition");
   foreach(var item in ContractJson.Read<OrderQuantity[]>(fulfillment.Items))
   {var line=order.Items.Single(i=>i.VariantId==item.VariantId);if(request.Status is FulfillmentStatus.Cancelled or FulfillmentStatus.Failed)line.ShipmentRequested-=item.Quantity;if(request.Status==FulfillmentStatus.Shipped)line.Fulfilled+=item.Quantity;if(request.Status==FulfillmentStatus.Delivered)line.Delivered+=item.Quantity;}
   fulfillment.Status=request.Status;fulfillment.CourierReference=request.CourierReference is null?fulfillment.CourierReference:OrderRules.Text(request.CourierReference,160);
   if(request.Status==FulfillmentStatus.Shipped){var target=order.Items.All(i=>i.Fulfilled==i.Quantity)?OrderStatus.Fulfilled:OrderStatus.PartiallyFulfilled;if(order.Status!=target)Transition(order,target,"Shipping dispatch confirmed");}
   if(request.Status==FulfillmentStatus.Delivered){order.DeliveredAt??=Now;if(order.Items.All(i=>i.Delivered==i.Quantity)){Transition(order,OrderStatus.Delivered,"Delivery confirmed");order.DeliveredAt=Now;}}
   return await RecordResource(order,request.OperationId,"order.fulfillment.status",hash,()=>View(fulfillment),token);
  },ct);
 }
 public async Task<object> ShipmentContext(Guid tenant,Guid id,CancellationToken ct)
 {access.Recovery(tenant,["Shipping"]);var value=await repository.Fulfillment(id,ct)??throw new BusinessFault("FulfillmentNotFound",404);var order=await repository.Order(value.OrderId,ct)??throw new BusinessFault("OrderNotFound",404);return new{Fulfillment=View(value),Order=View(order),order.ShippingQuoteId};}
}
