using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record ShippingQuoteSnapshot(Guid Id,Guid TenantId,Guid MethodId,string MethodName,string Currency,string CountryCode,string PostalCode,OrderQuantity[] Items,decimal Net,decimal Tax,decimal Total,DateTimeOffset ExpiresAt,Guid? PickupWarehouseId,Guid? PreparationId=null,Guid? UserId=null);
