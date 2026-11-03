namespace SRECommerce.Shipping.Application;
public sealed record ShippingQuoteContract(Guid Id,Guid TenantId,Guid MethodId,string MethodName,string Currency,string CountryCode,string PostalCode,ShippingQuantity[] Items,decimal Net,decimal Tax,decimal Total,DateTimeOffset ExpiresAt,Guid? PickupWarehouseId,Guid PreparationId,Guid? UserId);
