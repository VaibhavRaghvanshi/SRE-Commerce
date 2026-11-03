namespace SRECommerce.Shipping.Application;
public sealed record ShippingOrderProof(Guid Id,int Status,int PaymentState,bool Historical,ShippingContact Contact,ShippingQuantity[] Items);
