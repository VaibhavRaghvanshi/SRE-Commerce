using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed record ReturnPickupView(Guid Id,Guid ReturnId,Guid OrderId,Guid WarehouseId,DateTimeOffset WindowFrom,DateTimeOffset WindowTo,PickupStatus Status,string Note,string Version);
