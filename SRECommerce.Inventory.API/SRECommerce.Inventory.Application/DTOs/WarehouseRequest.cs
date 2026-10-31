namespace SRECommerce.Inventory.Application;
public record WarehouseRequest(Guid BranchId,string Code,string Name,bool Active=true,string? Version=null);
