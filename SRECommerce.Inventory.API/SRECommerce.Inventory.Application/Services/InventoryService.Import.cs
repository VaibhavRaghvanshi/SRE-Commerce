using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public sealed partial class InventoryService
{
    public async Task<Guid[]> ImportOpeningStock(Guid tenant,OpeningStockImport request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);Id(request.Key);Id(request.WarehouseId);
        var source=InventoryRules.Text(request.Source,80);var lot=InventoryRules.Code(request.Lot);
        if(request.Rows is null||request.Rows.Length is <1 or >100||request.Rows.Select(r=>r.VariantId).Distinct().Count()!=request.Rows.Length)throw new BusinessFault("InvalidOpeningStockImport",400);
        foreach(var row in request.Rows){Id(row.ProductId);Id(row.VariantId);InventoryRules.Quantity(row.Quantity,false);if(row.Quantity<0)throw new BusinessFault("InvalidOpeningQuantity",400);}
        var hash=Hash(new{Action="opening-import",request});
        return await repository.Transaction(async token=>
        {
            var prior=await repository.Operation(request.Key,token);if(prior is not null){if(prior.PayloadHash!=hash)throw new BusinessFault("OperationKeyReused");return ContractJson.Read<Guid[]>(prior.Result);}
            await WarehouseAvailable(request.WarehouseId,token);
            var catalog=await access.CatalogItems(tenant,request.Rows.Select(r=>r.VariantId).ToArray(),token);var items=await repository.Items(token);
            foreach(var row in request.Rows)
            {
                var sku=catalog.SingleOrDefault(s=>s.VariantId==row.VariantId&&s.ProductId==row.ProductId)??throw new BusinessFault("CatalogVariantUnavailable");
                // Opening imports create new tracked identities. Existing balances must use explicit, versioned stock changes.
                if(items.Any(i=>i.Id==row.VariantId))throw new BusinessFault("OpeningStockAlreadyTracked");
                repository.Add(new StockItem{TenantId=tenant,Id=sku.VariantId,ProductId=sku.ProductId,Sku=sku.Sku,Barcode=sku.Barcode});
                if(row.Quantity>0)await ReceiveStock(tenant,request.WarehouseId,row.VariantId,lot,null,row.Quantity,request.Key,"Opening import: "+source,token);
            }
            var ids=request.Rows.Select(r=>r.VariantId).ToArray();repository.Add(new StockOperation{TenantId=tenant,Id=request.Key,Action=StockAction.Receipt,PayloadHash=hash,CreatedAt=Now,Result=ContractJson.Write(ids)});
            await repository.Commit("inventory.opening.imported",request.Key,new{request.Key,Source=source,request.WarehouseId,VariantIds=ids},token);return ids;
        },ct);
    }
}
