using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;

public sealed partial class InventoryService(IInventoryRepository repository,IInventoryAccess access,TimeProvider clock):IInventoryService,IInventoryMaintenance
{
    private DateTimeOffset Now=>clock.GetUtcNow();
    private static string Version(byte[] value)=>Convert.ToBase64String(value);
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(value))));
    private static void Id(Guid id){if(id==Guid.Empty)throw new BusinessFault("InvalidInventoryId",400);}
    private static void Page(int page){if(page is <1 or >10000)throw new BusinessFault("InvalidInventoryPage",400);}
    private static object BranchView(Branch b)=>new{b.Id,b.Code,b.Name,b.TenantLocationId,b.Active,Version=Version(b.Version)};
    private static object WarehouseView(Warehouse w)=>new{w.Id,w.BranchId,w.Code,w.Name,w.Active,Version=Version(w.Version)};
    private static object ItemView(StockItem i)=>new{i.Id,i.ProductId,i.Sku,i.Barcode,i.Active,i.ReorderLevel,Version=Version(i.Version)};
    private static object SupplierView(Supplier s)=>new{s.Id,s.Code,s.Name,s.ContactEmail,s.Active,Version=Version(s.Version)};
    private BalanceView View(InventoryStock stock)=>new(stock.Balance.Id,stock.Balance.WarehouseId,stock.Balance.VariantId,stock.Batch.Id,stock.Batch.Lot,stock.Batch.ExpiresAt,stock.Balance.OnHand,stock.Balance.Reserved,stock.Balance.Damaged,stock.Batch.ExpiresAt<=Now?0:stock.Balance.OnHand-stock.Balance.Reserved,Version(stock.Balance.Version));
    private static ReservationView View(StockReservation r)=>new(r.Id,r.WarehouseId,r.Reference,r.Status,r.ExpiresAt,r.Lines.Select(l=>new ReservationAllocation(l.VariantId,l.BalanceId,l.Quantity)).ToArray(),Version(r.Version));
    private static PurchaseView View(PurchaseOrder p)=>new(p.Id,p.SupplierId,p.WarehouseId,p.Reference,p.Currency,p.Status,p.Lines.Select(l=>new PurchaseLineView(l.Id,l.VariantId,l.Ordered,l.Received,l.UnitCost)).ToArray(),Version(p.Version));
    private async Task<Warehouse> WarehouseAvailable(Guid id,CancellationToken ct)
    {var warehouse=(await repository.Warehouses(ct)).SingleOrDefault(w=>w.Id==id&&w.Active)??throw new BusinessFault("WarehouseUnavailable");if(!(await repository.Branches(ct)).Any(b=>b.Id==warehouse.BranchId&&b.Active))throw new BusinessFault("BranchUnavailable");return warehouse;}
    private async Task<StockItem> ItemAvailable(Guid id,CancellationToken ct)=>(await repository.Items(ct)).SingleOrDefault(i=>i.Id==id&&i.Active)??throw new BusinessFault("StockItemUnavailable");
    public async Task<object> Lookups(Guid tenant,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Staff,ct);return new{Branches=(await repository.Branches(ct)).Select(BranchView),Warehouses=(await repository.Warehouses(ct)).Select(WarehouseView),Items=(await repository.Items(ct)).Select(ItemView),Suppliers=(await repository.Suppliers(ct)).Select(SupplierView)};}
    public async Task<object> Branch(Guid tenant,Guid? id,BranchRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);
        return await repository.Transaction(async token=>
        {
            var value=id is null?new Branch{TenantId=tenant}:(await repository.Branches(token)).SingleOrDefault(b=>b.Id==id)??throw new BusinessFault("BranchNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            if(!request.Active&&(await repository.Warehouses(token)).Any(w=>w.BranchId==value.Id&&w.Active))throw new BusinessFault("ActiveWarehousesRemain");
            if(request.TenantLocationId==Guid.Empty)throw new BusinessFault("InvalidLocationReference",400);
            value.Code=InventoryRules.Code(request.Code);value.Name=InventoryRules.Text(request.Name);value.TenantLocationId=request.TenantLocationId;value.Active=request.Active;
            await repository.Commit("inventory.branch.saved",value.Id,null,token);return BranchView(value);
        },ct);
    }
    public async Task<object> Warehouse(Guid tenant,Guid? id,WarehouseRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);
        return await repository.Transaction(async token=>
        {
            if(!(await repository.Branches(token)).Any(b=>b.Id==request.BranchId&&b.Active))throw new BusinessFault("BranchUnavailable");
            var value=id is null?new Warehouse{TenantId=tenant}:(await repository.Warehouses(token)).SingleOrDefault(w=>w.Id==id)??throw new BusinessFault("WarehouseNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            if(id is not null&&(value.BranchId!=request.BranchId||!request.Active)&&(await repository.Stock(value.Id,null,token)).Any(s=>s.Balance.OnHand+s.Balance.Damaged>0))throw new BusinessFault("WarehouseContainsStock");
            value.BranchId=request.BranchId;value.Code=InventoryRules.Code(request.Code);value.Name=InventoryRules.Text(request.Name);value.Active=request.Active;
            await repository.Commit("inventory.warehouse.saved",value.Id,null,token);return WarehouseView(value);
        },ct);
    }
    public async Task<object> Register(Guid tenant,RegisterItemRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);Id(request.ProductId);Id(request.VariantId);InventoryRules.Quantity(request.ReorderLevel,false);if(request.ReorderLevel<0)throw new BusinessFault("InvalidReorderLevel",400);
        var catalog=await access.CatalogItem(tenant,request.ProductId,request.VariantId,ct);
        return await repository.Transaction(async token=>
        {
            var value=(await repository.Items(token)).SingleOrDefault(i=>i.Id==catalog.VariantId);
            if(value is null){value=new StockItem{TenantId=tenant,Id=catalog.VariantId,ProductId=catalog.ProductId,Sku=catalog.Sku,Barcode=catalog.Barcode};repository.Add(value);}
            else{repository.Version(value,request.Version);if(value.ProductId!=catalog.ProductId||value.Sku!=catalog.Sku)throw new BusinessFault("StockIdentityMismatch");}
            value.ReorderLevel=request.ReorderLevel;value.Barcode=catalog.Barcode;await repository.Commit("inventory.item.registered",value.Id,null,token);return ItemView(value);
        },ct);
    }
    public async Task<object> Supplier(Guid tenant,Guid? id,SupplierRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);
        return await repository.Transaction(async token=>
        {
            var value=id is null?new Supplier{TenantId=tenant}:(await repository.Suppliers(token)).SingleOrDefault(s=>s.Id==id)??throw new BusinessFault("SupplierNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            if(request.ContactEmail is not null&&(!System.Net.Mail.MailAddress.TryCreate(request.ContactEmail,out var email)||email.Address!=request.ContactEmail||request.ContactEmail.Length>254))throw new BusinessFault("InvalidSupplierEmail",400);
            value.Code=InventoryRules.Code(request.Code);value.Name=InventoryRules.Text(request.Name);value.ContactEmail=request.ContactEmail;value.Active=request.Active;
            await repository.Commit("inventory.supplier.saved",value.Id,null,token);return SupplierView(value);
        },ct);
    }
    public async Task<BalanceView[]> Balances(Guid tenant,Guid? warehouse,Guid? variant,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Staff,ct);return(await repository.Stock(warehouse,variant is {} id?[id]:null,ct)).Select(View).ToArray();}
    public async Task<object> Movements(Guid tenant,Guid? balance,int page,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Manager,ct);Page(page);return await repository.Movements(balance,page,ct);}
    public async Task<AvailabilityView[]> Availability(Guid tenant,Guid[] variants,CancellationToken ct)
    {
        await access.Public(tenant,ct);if(variants.Length is <1 or >100||variants.Any(v=>v==Guid.Empty)||variants.Distinct().Count()!=variants.Length)throw new BusinessFault("InvalidAvailabilityQuery",400);
        var branches=(await repository.Branches(ct)).Where(b=>b.Active).Select(b=>b.Id).ToHashSet();var warehouses=(await repository.Warehouses(ct)).Where(w=>w.Active&&branches.Contains(w.BranchId)).Select(w=>w.Id).ToHashSet();
        var visible=(await access.PublicVariants(tenant,variants,ct)).ToHashSet();var stock=await repository.Stock(null,variants,ct);var items=await repository.Items(ct);
        return variants.Select(id=>{var item=items.SingleOrDefault(i=>i.Id==id&&i.Active&&visible.Contains(id));var quantity=item is null?0:stock.Where(s=>s.Balance.VariantId==id&&warehouses.Contains(s.Balance.WarehouseId)&&(s.Batch.ExpiresAt is null||s.Batch.ExpiresAt>Now)).Sum(s=>s.Balance.OnHand-s.Balance.Reserved);return new AvailabilityView(id,quantity,item is not null&&quantity<=item.ReorderLevel,item is not null);}).ToArray();
    }
}
