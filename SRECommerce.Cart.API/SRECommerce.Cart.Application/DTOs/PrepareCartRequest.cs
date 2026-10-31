namespace SRECommerce.Cart.Application;
public sealed record PrepareCartRequest(Guid Id,string Version,string? CouponCode=null);
