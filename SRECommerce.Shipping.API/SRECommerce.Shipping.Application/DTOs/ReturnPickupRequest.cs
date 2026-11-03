namespace SRECommerce.Shipping.Application;
public sealed record ReturnPickupRequest(Guid Id,Guid ReturnId,Guid WarehouseId,DateTimeOffset WindowFrom,DateTimeOffset WindowTo);
