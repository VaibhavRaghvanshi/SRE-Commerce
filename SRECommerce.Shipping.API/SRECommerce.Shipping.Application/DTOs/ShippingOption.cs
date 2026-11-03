using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed record ShippingOption(Guid Id,Guid MethodId,string MethodName,string Description,DeliveryKind Kind,string Currency,string CountryCode,string PostalCode,decimal Net,decimal Tax,decimal Total,DateTimeOffset ExpiresAt,Guid PreparationId);
