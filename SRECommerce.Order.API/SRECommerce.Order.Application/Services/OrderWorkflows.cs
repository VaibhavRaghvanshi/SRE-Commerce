using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Kernel;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed class OrderWorkflows(IOrderRepository repository,IOrderDependencies dependencies,TimeProvider clock):IOrderWorkflows
{
 private DateTimeOffset Now=>clock.GetUtcNow();
 public async Task Run(CancellationToken ct)
 {
  foreach(var fulfillment in await repository.PendingFulfillments(Now,ct))
  {
   try{await dependencies.CreateShipment(fulfillment.TenantId,fulfillment.Id,ct);await repository.Transaction(async token=>{if(fulfillment.Status!=FulfillmentStatus.Requested)return false;fulfillment.Status=FulfillmentStatus.Created;fulfillment.LastFault=null;await repository.Commit("order.fulfillment.created",fulfillment.Id,new{FulfillmentId=fulfillment.Id,fulfillment.OrderId},token);return true;},ct);}
   catch(Exception error)when(error is BusinessFault or DependencyFault or DomainFault)
   {await repository.Transaction(async token=>{fulfillment.Attempts++;fulfillment.LastFault=Fault(error);fulfillment.NextAttemptAt=Now.AddSeconds(Delay(fulfillment.Attempts));if(fulfillment.Attempts>=12){fulfillment.Status=FulfillmentStatus.Failed;var order=await repository.Order(fulfillment.OrderId,token)??throw new BusinessFault("OrderNotFound",404);foreach(var line in ContractJson.Read<OrderQuantity[]>(fulfillment.Items))order.Items.Single(i=>i.VariantId==line.VariantId).ShipmentRequested-=line.Quantity;order.Revision++;order.UpdatedAt=Now;}await repository.Commit("order.fulfillment.recovery",fulfillment.Id,null,token);return true;},ct);}
  }
  foreach(var order in await repository.PendingCOD(ct))
  {try{var payment=await dependencies.Payment(order.TenantId,order.Id,ct)??throw new DependencyFault("Payment");if(payment.Status==7)payment=await dependencies.CapturePayment(order.TenantId,order.Id,payment.Version,ct);if(payment.Status==2){order.PaymentState=PaymentState.Captured;order.Revision++;order.UpdatedAt=Now;await repository.Commit("order.cod.settled",order.Id,new{OrderId=order.Id},ct);}}catch(Exception error)when(error is BusinessFault or DependencyFault or DomainFault){/* A rejected/unavailable settlement cannot block other orders or invent a receipt. */}}
  foreach(var returned in await repository.PendingReturns(Now,ct))
  {
   try
   {
    var order=await repository.Order(returned.OrderId,ct)??throw new BusinessFault("OrderNotFound",404);var lines=ContractJson.Read<ReturnLine[]>(returned.Items);
    if(returned.Status==ReturnStatus.Receiving)
    {
     if(returned.WarehouseId is not {} warehouse)throw new BusinessFault("ReturnWarehouseRequired");
     await dependencies.ReceiveReturn(returned.TenantId,OrderRules.Operation(returned.Id,"receipt"),order.Id,warehouse,lines.Select(i=>new OrderQuantity(i.VariantId,i.Quantity)).ToArray(),returned.Resalable,ct);
     await repository.Transaction(async token=>{if(!returned.ReceiptRecorded){foreach(var line in lines)order.Items.Single(i=>i.VariantId==line.VariantId).Returned+=line.Quantity;returned.ReceiptRecorded=true;}returned.Status=ReturnStatus.RefundPending;returned.Attempts=0;returned.LastFault=null;order.Revision++;order.UpdatedAt=Now;if(returned.ExchangeId is {} exchangeId&&await repository.Exchange(exchangeId,token) is {} exchange)exchange.Status=ExchangeStatus.ReturnReceived;OrderRules.Lines(order);await repository.Commit("order.return.received",returned.Id,new{ReturnId=returned.Id,OrderId=order.Id},token);return true;},ct);
    }
    else
    {
     var payment=await dependencies.Payment(returned.TenantId,order.Id,ct)??throw new DependencyFault("Payment");if(payment.Status is not(2 or 5 or 6)&&returned.RefundAmount>0){returned.NextAttemptAt=Now.AddSeconds(10);await repository.Commit("order.return.awaiting.settlement",returned.Id,null,ct);continue;}
     if(returned.RefundAmount>0)await dependencies.Refund(returned.TenantId,order.Id,OrderRules.Operation(returned.Id,"refund"),returned.RefundAmount,"Approved customer return",ct);
     payment=await dependencies.Payment(returned.TenantId,order.Id,ct)??throw new DependencyFault("Payment");
     await repository.Transaction(async token=>{returned.Status=ReturnStatus.Refunded;returned.Attempts=0;returned.LastFault=null;order.Refunded=payment.Refunded;order.PaymentState=payment.Status==6?PaymentState.Refunded:payment.Status==5?PaymentState.PartiallyRefunded:order.PaymentState;order.Revision++;order.UpdatedAt=Now;await repository.Commit("order.return.refunded",returned.Id,new{ReturnId=returned.Id,OrderId=order.Id,returned.RefundAmount},token);return true;},ct);
    }
   }
   catch(Exception error)when(error is BusinessFault or DependencyFault or DomainFault)
   {returned.Attempts++;returned.LastFault=Fault(error);returned.NextAttemptAt=Now.AddSeconds(Delay(returned.Attempts));if(returned.Attempts>=12)returned.Status=ReturnStatus.ManualReview;await repository.Commit("order.return.recovery",returned.Id,null,ct);}
  }
 }
 private static double Delay(int attempts)=>Math.Min(60,Math.Pow(2,Math.Min(attempts,5)));
 private static string Fault(Exception error)=>error is BusinessFault rule?rule.Code:error is DomainFault domain?domain.Code:error is DependencyFault dependency?dependency.Dependency:"OrderWorkflowUnavailable";
}
