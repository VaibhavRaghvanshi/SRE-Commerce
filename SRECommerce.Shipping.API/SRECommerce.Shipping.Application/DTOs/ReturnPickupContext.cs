namespace SRECommerce.Shipping.Application;
public sealed record ReturnPickupContext(ReturnPickupProof Return,ShippingOrderProof Order);
