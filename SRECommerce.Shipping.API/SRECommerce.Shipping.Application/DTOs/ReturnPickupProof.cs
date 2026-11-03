namespace SRECommerce.Shipping.Application;
public sealed record ReturnPickupProof(Guid Id,Guid OrderId,ShippingQuantity[] Items,int Status);
