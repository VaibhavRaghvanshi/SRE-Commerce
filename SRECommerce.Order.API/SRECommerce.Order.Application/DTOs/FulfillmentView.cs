using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record FulfillmentView(Guid Id,Guid OrderId,Guid WarehouseId,OrderQuantity[] Items,FulfillmentStatus Status,string? CourierReference,string Version);
