using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed record ShipmentView(Guid Id,Guid OrderId,Guid WarehouseId,DeliveryKind Kind,string Provider,string CourierReference,ShipmentStatus Status,ShippingQuantity[] Items,DateTimeOffset CreatedAt,DateTimeOffset? DispatchedAt,DateTimeOffset? DeliveredAt,string Version);
