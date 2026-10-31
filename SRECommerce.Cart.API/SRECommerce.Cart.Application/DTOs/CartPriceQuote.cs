namespace SRECommerce.Cart.Application;
public sealed record CartPriceQuote(Guid Id,Guid TenantId,Guid? UserId,string Currency,int DecimalPlaces,DateTimeOffset ExpiresAt,Guid? CouponId,CartCalculation Calculation);
