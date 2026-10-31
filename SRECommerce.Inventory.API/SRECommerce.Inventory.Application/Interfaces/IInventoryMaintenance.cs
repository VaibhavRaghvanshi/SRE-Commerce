namespace SRECommerce.Inventory.Application;
// Called only in a trusted, tenant-bound maintenance scope, never a public endpoint.
public interface IInventoryMaintenance { Task<int> ExpireBound(CancellationToken ct); }
