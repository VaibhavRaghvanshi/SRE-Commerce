namespace SRECommerce.Cart.Application;
public sealed record CartCalculation(Guid? PromotionId,string? PromotionCode,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal Total,CartCalculatedLine[] Lines);
