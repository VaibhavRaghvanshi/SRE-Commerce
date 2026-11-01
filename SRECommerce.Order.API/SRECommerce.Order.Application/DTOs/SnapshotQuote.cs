using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotQuote(Guid Id,Guid TenantId,Guid? UserId,string Currency,int DecimalPlaces,DateTimeOffset ExpiresAt,Guid? CouponId,SnapshotCalculation Calculation);
