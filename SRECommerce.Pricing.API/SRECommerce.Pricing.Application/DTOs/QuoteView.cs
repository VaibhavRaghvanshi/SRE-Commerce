using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public record QuoteView(Guid Id,Guid TenantId,Guid? UserId,string Currency,int DecimalPlaces,DateTimeOffset ExpiresAt,Guid? CouponId,PriceCalculation Calculation);
