using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public sealed partial class InventoryService
{
 public async Task<object> Abort(Guid tenant,Guid reservation,ReservationAbortRequest request,CancellationToken ct)
 {
  access.Recovery(tenant,["Order","Cart","Manufacturing"]);Id(reservation);Id(request.OperationId);var reason=InventoryRules.Text(request.Reason,300);var source=access.MachineSource??throw new BusinessFault("ServiceIdentityRequired",401);var hash=Hash(new{Action="reservation-cancel",ReservationId=reservation,request});
  if(source=="Manufacturing"&&request.OperationId!=reservation)throw new BusinessFault("ProductionCancellationMustUseProductionId",400);
  return await repository.Transaction(async token=>
  {
   var fence=await repository.Cancellation(reservation,token);if(fence is not null&&fence.SourceService!=source)throw new BusinessFault("ReservationOwnerMismatch",403);if(fence is not null&&fence.OperationId!=request.OperationId)throw new BusinessFault("ReservationCancellationKeyReused");
   var value=await repository.Reservation(reservation,token);if(value is not null)ReservationOwner(value,true);
   var previous=await repository.Operation(request.OperationId,token);if(previous is not null){if(previous.PayloadHash!=hash||previous.Action!=StockAction.Compensation)throw new BusinessFault("OperationKeyReused");return ContractJson.Read<System.Text.Json.JsonElement>(previous.Result);}if(value is not null)
   {
    ReservationOwner(value,true);
    if(value.Status==ReservationStatus.Active)await Finish(tenant,value,ReservationStatus.Released,token);
    else if(value.Status==ReservationStatus.Committed)
    {
     var stock=await repository.Stock(value.WarehouseId,value.Lines.Select(l=>l.VariantId).Distinct().ToArray(),token);
     foreach(var line in value.Lines){var balance=stock.SingleOrDefault(s=>s.Balance.Id==line.BalanceId)?.Balance??throw new BusinessFault("ReservedBalanceUnavailable");Move(tenant,request.OperationId,balance,StockAction.Compensation,line.Quantity,0,0,reason);}
     value.RestoreOperationId=request.OperationId;value.RestoredAt=Now;value.Status=ReservationStatus.Compensated;
    }
   }
   if(fence is null)repository.Add(new ReservationCancellation{TenantId=tenant,Id=reservation,SourceService=source,OperationId=request.OperationId,CreatedAt=Now});
   var result=new{ReservationId=reservation,Cancelled=true,Restored=value?.Status==ReservationStatus.Compensated};repository.Add(new StockOperation{TenantId=tenant,Id=request.OperationId,Action=StockAction.Compensation,PayloadHash=hash,CreatedAt=Now,Result=ContractJson.Write(result)});
   await repository.Commit("inventory.checkout.compensated",reservation,result,token);return (object)result;
  },ct);
 }
 public async Task<BalanceView[]> CustomerReturn(Guid tenant,CustomerReturnReceipt request,CancellationToken ct)
 {
  access.Recovery(tenant,["Order"]);Id(request.OperationId);Id(request.OrderId);Id(request.WarehouseId);
  if(request.Items is null||request.Items.Length is <1 or >100||request.Items.Select(i=>i.VariantId).Distinct().Count()!=request.Items.Length)throw new BusinessFault("InvalidCustomerReturnItems",400);
  foreach(var item in request.Items){Id(item.VariantId);InventoryRules.Quantity(item.Quantity);}var hash=Hash(new{Action="customer-return",request});
  return await repository.Transaction(async token=>
  {
   var previous=await repository.Operation(request.OperationId,token);if(previous is not null){if(previous.PayloadHash!=hash||previous.Action!=StockAction.CustomerReturn)throw new BusinessFault("OperationKeyReused");return ContractJson.Read<BalanceView[]>(previous.Result);}
   await WarehouseAvailable(request.WarehouseId,token);var items=await repository.Items(token);var received=new List<InventoryStock>();
   foreach(var item in request.Items)
   {
    if(!items.Any(i=>i.Id==item.VariantId))throw new BusinessFault("StockItemUnavailable");var batch=new InventoryBatch{TenantId=tenant,WarehouseId=request.WarehouseId,VariantId=item.VariantId,Lot="RETURN-"+request.OperationId.ToString("N").ToUpperInvariant(),ReceivedAt=Now};var balance=new StockBalance{TenantId=tenant,WarehouseId=request.WarehouseId,VariantId=item.VariantId,BatchId=batch.Id};repository.Add(batch);repository.Add(balance);
    Move(tenant,request.OperationId,balance,StockAction.CustomerReturn,request.Resalable?item.Quantity:0,0,request.Resalable?0:item.Quantity,"Customer return "+request.OrderId.ToString("N"));received.Add(new(balance,batch));
   }
   await repository.Commit("inventory.customer.return.received",request.OperationId,new{request.OperationId,request.OrderId,request.WarehouseId,request.Resalable},token);var result=received.Select(View).ToArray();repository.Add(new StockOperation{TenantId=tenant,Id=request.OperationId,Action=StockAction.CustomerReturn,PayloadHash=hash,CreatedAt=Now,Result=ContractJson.Write(result)});await repository.Commit("inventory.operation.recorded",request.OperationId,null,token);return result;
  },ct);
 }
}
