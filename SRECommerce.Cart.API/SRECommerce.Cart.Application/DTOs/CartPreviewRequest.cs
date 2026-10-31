namespace SRECommerce.Cart.Application;
public sealed record CartPreviewRequest(Guid Id,string? CouponCode=null);
