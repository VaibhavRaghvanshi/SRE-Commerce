namespace SRECommerce.Shipping.Application;
public sealed record PreparedShippingCart(Guid Id,Guid CartId,Guid TenantId,Guid? UserId,int CartRevision,DateTimeOffset ExpiresAt,ShippingQuantity[] Items,ShippingCartQuote Quote,string? CustomerRole);
