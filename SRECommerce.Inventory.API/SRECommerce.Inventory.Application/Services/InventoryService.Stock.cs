using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public sealed partial class InventoryService
{
    private void Move(Guid tenant,Guid operation,StockBalance balance,StockAction action,decimal onHand,decimal reserved,decimal damaged,string reason)
    {
        InventoryRules.Balance(balance.OnHand+onHand,balance.Reserved+reserved,balance.Damaged+damaged);
        balance.OnHand+=onHand;balance.Reserved+=reserved;balance.Damaged+=damaged;
        repository.Add(new InventoryMovement{TenantId=tenant,OperationId=operation,BalanceId=balance.Id,Action=action,OnHandDelta=onHand,ReservedDelta=reserved,DamagedDelta=damaged,Reason=reason,OccurredAt=Now});
    }
    private async Task<InventoryStock> ReceiveStock(Guid tenant,Guid warehouse,Guid variant,string lot,DateTimeOffset? expiry,decimal quantity,Guid operation,string reason,CancellationToken ct)
    {
        InventoryRules.Quantity(quantity);lot=InventoryRules.Code(lot);if(expiry<=Now)throw new BusinessFault("ReceiptAlreadyExpired",400);
        var stock=(await repository.Stock(warehouse,[variant],ct)).SingleOrDefault(s=>s.Batch.Lot==lot);
        if(stock is null)
        {
            var batch=new InventoryBatch{TenantId=tenant,WarehouseId=warehouse,VariantId=variant,Lot=lot,ExpiresAt=expiry,ReceivedAt=Now};
            var balance=new StockBalance{TenantId=tenant,WarehouseId=warehouse,VariantId=variant,BatchId=batch.Id};repository.Add(batch);repository.Add(balance);stock=new(balance,batch);
        }
        else if(stock.Batch.ExpiresAt!=expiry)throw new BusinessFault("LotExpiryMismatch");
        Move(tenant,operation,stock.Balance,StockAction.Receipt,quantity,0,0,reason);return stock;
    }
    public async Task<BalanceView> Change(Guid tenant,StockChangeRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);Id(request.OperationId);var hash=Hash(request);var reason=InventoryRules.Text(request.Reason,300);
        if(request.Action is not(StockAction.Receipt or StockAction.Adjustment or StockAction.Damage or StockAction.ReturnToSale or StockAction.Reconciliation))throw new BusinessFault("InvalidStockAction",400);
        InventoryRules.Quantity(request.Quantity,request.Action is StockAction.Receipt or StockAction.Damage or StockAction.ReturnToSale);if(request.Action==StockAction.Adjustment&&request.Quantity==0)throw new BusinessFault("EmptyAdjustment",400);
        return await repository.Transaction(async token=>
        {
            var previous=await repository.Operation(request.OperationId,token);if(previous is not null){if(previous.PayloadHash!=hash||previous.Action!=request.Action)throw new BusinessFault("OperationKeyReused");return ContractJson.Read<BalanceView>(previous.Result);}
            await WarehouseAvailable(request.WarehouseId,token);await ItemAvailable(request.VariantId,token);InventoryStock stock;
            if(request.Action==StockAction.Receipt)
            {
                if(request.BatchId is not null||request.Version is not null)throw new BusinessFault("ReceiptUsesLotIdentity",400);
                stock=await ReceiveStock(tenant,request.WarehouseId,request.VariantId,request.Lot??"UNBATCHED",request.ExpiresAt,request.Quantity,request.OperationId,reason,token);
            }
            else
            {
                stock=(await repository.Stock(request.WarehouseId,[request.VariantId],token)).SingleOrDefault(s=>s.Batch.Id==request.BatchId)??throw new BusinessFault("InventoryBatchNotFound",404);repository.Version(stock.Balance,request.Version);
                switch(request.Action)
                {
                    case StockAction.Adjustment:Move(tenant,request.OperationId,stock.Balance,request.Action,request.Quantity,0,0,reason);break;
                    case StockAction.Damage:Move(tenant,request.OperationId,stock.Balance,request.Action,-request.Quantity,0,request.Quantity,reason);break;
                    case StockAction.ReturnToSale:if(stock.Batch.ExpiresAt<=Now)throw new BusinessFault("ExpiredLotCannotReturnToSale");Move(tenant,request.OperationId,stock.Balance,request.Action,request.Quantity,0,-request.Quantity,reason);break;
                    case StockAction.Reconciliation:Move(tenant,request.OperationId,stock.Balance,request.Action,request.Quantity-stock.Balance.OnHand,0,0,reason);break;
                }
            }
            // Rowversion is assigned by SQL; retain a stable result for exact idempotent replays.
            await repository.Commit("inventory.stock.changed",request.OperationId,new{request.OperationId,request.VariantId,request.WarehouseId,request.Action},token);
            var result=View(stock);repository.Add(new StockOperation{TenantId=tenant,Id=request.OperationId,Action=request.Action,PayloadHash=hash,CreatedAt=Now,Result=ContractJson.Write(result)});await repository.Commit("inventory.operation.recorded",request.OperationId,null,token);return result;
        },ct);
    }
}
