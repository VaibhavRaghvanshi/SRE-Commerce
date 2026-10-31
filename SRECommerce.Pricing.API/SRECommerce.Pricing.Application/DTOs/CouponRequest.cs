namespace SRECommerce.Pricing.Application;
public record CouponRequest(string Code,Guid PromotionId,int GlobalLimit,int PerCustomerLimit,bool Active=true,string? Version=null);
