using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public record RedemptionView(Guid OrderId,Guid QuoteId,Guid? CouponId,RedemptionStatus Status,DateTimeOffset ExpiresAt,string Version);
