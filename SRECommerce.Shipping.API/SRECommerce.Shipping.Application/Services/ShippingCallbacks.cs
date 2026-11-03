using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed class ShippingCallbacks(IShippingRepository repository,IShippingDependencies dependencies,TimeProvider clock) : IShippingCallbacks
{
    private DateTimeOffset Now=>clock.GetUtcNow();
    public async Task Run(CancellationToken ct)
    {
        foreach(var pending in await repository.Due(Now,ct))
        {
            var lease=Guid.NewGuid();
            var callback=await repository.Transaction(async token=>
            {
                var row=await repository.FreshCallback(pending.Id,token);if(row is null||row.Sent||row.ManualReview||row.LeaseUntil>Now||row.NextAttemptAt>Now)return null;
                row.LeaseId=lease;row.LeaseUntil=Now.AddSeconds(45);await repository.Save("shipping.callback.leased",row.Id,null,token);return row;
            },ct);
            if(callback is null)continue;
            try
            {
                if(callback.RequestDocument is null)
                {
                    var context=await dependencies.Fulfillment(callback.TenantId,callback.ShipmentId,ct);
                    if(context.Fulfillment.Status==0)throw new DependencyFault("OrderFulfillmentAcknowledgement");
                    var shipment=await repository.Shipment(callback.ShipmentId,ct)??throw new BusinessFault("ShipmentNotFound",404);
                    var document=ContractJson.Write(new OrderShipmentUpdate(callback.Id,context.Fulfillment.Version,callback.TargetStatus,shipment.CourierReference));
                    await repository.Transaction(async token=>
                    {
                        var current=await repository.FreshCallback(callback.Id,token)??throw new BusinessFault("ShipmentCallbackNotFound",404);
                        if(current.LeaseId!=lease)throw new BusinessFault("ShippingLeaseLost");current.RequestDocument=document;await repository.Save("shipping.callback.prepared",current.Id,null,token);return true;
                    },ct);
                }
                var update=ContractJson.Read<OrderShipmentUpdate>(callback.RequestDocument!);
                await dependencies.UpdateFulfillment(callback.TenantId,callback.ShipmentId,update,ct);
                await repository.Transaction(async token=>
                {
                    var current=await repository.FreshCallback(callback.Id,token)??throw new BusinessFault("ShipmentCallbackNotFound",404);
                    if(current.LeaseId!=lease)throw new BusinessFault("ShippingLeaseLost");current.Sent=true;current.LeaseId=null;current.LeaseUntil=null;current.LastFault=null;
                    await repository.Save("shipping.callback.sent",current.Id,null,token);return true;
                },ct);
            }
            catch(BusinessFault error)when(error.Code=="ShippingLeaseLost"){}
            catch(Exception error)when(error is BusinessFault or DependencyFault)
            {
                await repository.Transaction(async token=>
                {
                    var current=await repository.FreshCallback(callback.Id,token);if(current is null||current.LeaseId!=lease)return false;
                    current.LeaseId=null;current.LeaseUntil=null;current.Attempts++;current.LastFault=error is BusinessFault rule?rule.Code:((DependencyFault)error).Dependency;
                    current.NextAttemptAt=Now.AddSeconds(Math.Min(60,Math.Pow(2,Math.Min(5,current.Attempts))));if(current.Attempts>=12)current.ManualReview=true;
                    await repository.Save("shipping.callback.deferred",current.Id,null,token);return true;
                },ct);
            }
        }
    }
}
