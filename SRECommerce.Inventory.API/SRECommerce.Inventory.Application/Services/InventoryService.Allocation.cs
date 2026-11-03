using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public sealed partial class InventoryService
{
    public async Task<object> WarehouseForService(Guid tenant,Guid id,CancellationToken ct)
    {
        await access.Machine(tenant,["Shipping","Order","Manufacturing"],ct);Id(id);var warehouse=await WarehouseAvailable(id,ct);
        return new{warehouse.Id,warehouse.BranchId,warehouse.Code,warehouse.Name};
    }
    public async Task<object[]> WarehousesForService(Guid tenant,Guid[] ids,CancellationToken ct)
    {
        await access.Machine(tenant,["Shipping","Order","Manufacturing"],ct);
        if(ids.Length is <1 or >100||ids.Any(id=>id==Guid.Empty)||ids.Distinct().Count()!=ids.Length)throw new BusinessFault("InvalidWarehouseQuery",400);
        var branches=(await repository.Branches(ct)).Where(b=>b.Active).Select(b=>b.Id).ToHashSet();
        return (await repository.Warehouses(ct)).Where(w=>w.Active&&branches.Contains(w.BranchId)&&ids.Contains(w.Id)).Select(w=>(object)new{w.Id,w.BranchId,w.Code,w.Name}).ToArray();
    }
    public async Task<WarehouseAvailability[]> AllocationOptions(Guid tenant,Guid[] variants,DateTimeOffset expiresAt,CancellationToken ct)
    {
        await access.Machine(tenant,["Cart","Order","Manufacturing"],ct);InventoryRules.Reservation(Now,expiresAt);
        if(variants.Length is <1 or >100||variants.Any(v=>v==Guid.Empty)||variants.Distinct().Count()!=variants.Length)throw new BusinessFault("InvalidAllocationQuery",400);
        var branches=(await repository.Branches(ct)).Where(b=>b.Active).Select(b=>b.Id).ToHashSet();var items=(await repository.Items(ct)).Where(i=>i.Active).ToDictionary(i=>i.Id);
        var stock=await repository.Stock(null,variants,ct);var warehouses=(await repository.Warehouses(ct)).Where(w=>w.Active&&branches.Contains(w.BranchId)).OrderBy(w=>w.Code).ThenBy(w=>w.Id).ToArray();
        // A planner sees only lots that can remain valid throughout the requested reservation horizon.
        return warehouses.Select(warehouse=>new WarehouseAvailability(warehouse.Id,warehouse.BranchId,warehouse.Code,variants.Select(id=>
        {
            var tracked=items.TryGetValue(id,out var item);var available=tracked?stock.Where(s=>s.Balance.WarehouseId==warehouse.Id&&s.Balance.VariantId==id&&(s.Batch.ExpiresAt is null||s.Batch.ExpiresAt>expiresAt)).Sum(s=>s.Balance.OnHand-s.Balance.Reserved):0;
            return new AvailabilityView(id,available,tracked&&available<=item!.ReorderLevel,tracked);
        }).ToArray())).ToArray();
    }
}
