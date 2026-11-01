namespace SRECommerce.Inventory.Application;
public sealed record WarehouseAvailability(Guid WarehouseId,Guid BranchId,string Code,AvailabilityView[] Items);
