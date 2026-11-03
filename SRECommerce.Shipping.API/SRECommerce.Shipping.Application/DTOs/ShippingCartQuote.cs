namespace SRECommerce.Shipping.Application;
public sealed record ShippingCartQuote(Guid Id,Guid TenantId,Guid? UserId,string Currency,int DecimalPlaces,DateTimeOffset ExpiresAt,ShippingCartCalculation Calculation);
