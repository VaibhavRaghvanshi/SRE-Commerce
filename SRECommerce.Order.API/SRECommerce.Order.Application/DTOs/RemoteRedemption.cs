using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record RemoteRedemption(Guid OrderId,Guid QuoteId,Guid? CouponId,int Status,DateTimeOffset ExpiresAt,string Version);
