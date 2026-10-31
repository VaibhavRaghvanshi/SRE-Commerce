using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public sealed partial class InventoryService
{
    public async Task<PurchaseView> Purchase(Guid tenant,PurchaseRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);
        if(request.Lines is null||request.Lines.Length is <1 or >100||request.Lines.Select(l=>l.VariantId).Distinct().Count()!=request.Lines.Length)throw new BusinessFault("InvalidPurchaseLines",400);
        if(request.Currency.Length!=3||request.Currency.Any(c=>c is <'A' or >'Z'))throw new BusinessFault("InvalidPurchaseCurrency",400);
        foreach(var line in request.Lines){InventoryRules.Quantity(line.Quantity);if(line.UnitCost<0||line.UnitCost>1000000000m||decimal.Round(line.UnitCost,4)!=line.UnitCost)throw new BusinessFault("InvalidUnitCost",400);}
        return await repository.Transaction(async token=>
        {
            await WarehouseAvailable(request.WarehouseId,token);if(!(await repository.Suppliers(token)).Any(s=>s.Id==request.SupplierId&&s.Active))throw new BusinessFault("SupplierUnavailable");var items=await repository.Items(token);if(request.Lines.Any(l=>!items.Any(i=>i.Id==l.VariantId&&i.Active)))throw new BusinessFault("StockItemUnavailable");
            var purchase=new PurchaseOrder{TenantId=tenant,SupplierId=request.SupplierId,WarehouseId=request.WarehouseId,Reference=InventoryRules.Text(request.Reference,120),Currency=request.Currency,CreatedAt=Now};
            purchase.Lines=request.Lines.Select(l=>new PurchaseOrderLine{TenantId=tenant,PurchaseOrderId=purchase.Id,VariantId=l.VariantId,Ordered=l.Quantity,UnitCost=l.UnitCost}).ToList();repository.Add(purchase);
            await repository.Commit("inventory.purchase.created",purchase.Id,null,token);return View(purchase);
        },ct);
    }
    public async Task<PurchaseView[]> Purchases(Guid tenant,int page,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Manager,ct);Page(page);return(await repository.Purchases(page,ct)).Select(View).ToArray();}
    public async Task<PurchaseView> ChangePurchaseStatus(Guid tenant,Guid id,bool cancel,string version,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);
        return await repository.Transaction(async token=>
        {
            var value=await repository.Purchase(id,token)??throw new BusinessFault("PurchaseNotFound",404);repository.Version(value,version);
            if(cancel){if(value.Status is PurchaseStatus.Received or PurchaseStatus.Cancelled)throw new BusinessFault("PurchaseAlreadyFinal");value.Status=PurchaseStatus.Cancelled;}
            else{if(value.Status!=PurchaseStatus.Draft)throw new BusinessFault("PurchaseAlreadySubmitted");await WarehouseAvailable(value.WarehouseId,token);if(!(await repository.Suppliers(token)).Any(s=>s.Id==value.SupplierId&&s.Active))throw new BusinessFault("SupplierUnavailable");value.Status=PurchaseStatus.Ordered;}
            await repository.Commit(cancel?"inventory.purchase.cancelled":"inventory.purchase.ordered",value.Id,null,token);return View(value);
        },ct);
    }
    public async Task<PurchaseView> Receive(Guid tenant,Guid id,PurchaseReceiveRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);Id(request.OperationId);var hash=Hash(new{PurchaseId=id,Request=request});if(request.Lines is null||request.Lines.Length is <1 or >100||request.Lines.Select(l=>l.LineId).Distinct().Count()!=request.Lines.Length)throw new BusinessFault("InvalidReceiptLines",400);
        return await repository.Transaction(async token=>
        {
            var previous=await repository.Operation(request.OperationId,token);if(previous is not null){if(previous.PayloadHash!=hash||previous.Action!=StockAction.Receipt)throw new BusinessFault("OperationKeyReused");return ContractJson.Read<PurchaseView>(previous.Result);}
            var purchase=await repository.Purchase(id,token)??throw new BusinessFault("PurchaseNotFound",404);repository.Version(purchase,request.Version);if(purchase.Status is not(PurchaseStatus.Ordered or PurchaseStatus.PartiallyReceived))throw new BusinessFault("PurchaseNotReceivable");await WarehouseAvailable(purchase.WarehouseId,token);
            foreach(var input in request.Lines.OrderBy(l=>l.LineId))
            {
                InventoryRules.Quantity(input.Quantity);var line=purchase.Lines.SingleOrDefault(l=>l.Id==input.LineId)??throw new BusinessFault("PurchaseLineNotFound",404);if(line.Received+input.Quantity>line.Ordered)throw new BusinessFault("PurchaseOverReceipt");await ItemAvailable(line.VariantId,token);
                await ReceiveStock(tenant,purchase.WarehouseId,line.VariantId,input.Lot,input.ExpiresAt,input.Quantity,request.OperationId,"Purchase "+purchase.Reference,token);line.Received+=input.Quantity;
            }
            purchase.Status=purchase.Lines.All(l=>l.Received==l.Ordered)?PurchaseStatus.Received:PurchaseStatus.PartiallyReceived;purchase.ReceiptRevision++;
            await repository.Commit("inventory.purchase.received",purchase.Id,new{PurchaseId=id,request.OperationId},token);var result=View(purchase);
            repository.Add(new StockOperation{TenantId=tenant,Id=request.OperationId,Action=StockAction.Receipt,PayloadHash=hash,CreatedAt=Now,Result=ContractJson.Write(result)});await repository.Commit("inventory.operation.recorded",request.OperationId,null,token);return result;
        },ct);
    }
}
