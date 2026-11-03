namespace SRECommerce.Shipping.Application;
public sealed record FulfillmentProof(Guid Id,Guid OrderId,Guid WarehouseId,ShippingQuantity[] Items,int Status,string? CourierReference,string Version);
