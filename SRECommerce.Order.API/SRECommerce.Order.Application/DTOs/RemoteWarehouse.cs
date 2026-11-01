using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record RemoteWarehouse(Guid WarehouseId,Guid BranchId,string Code,RemoteAvailability[] Items);
