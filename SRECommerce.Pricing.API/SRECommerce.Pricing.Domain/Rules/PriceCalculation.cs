namespace SRECommerce.Pricing.Domain;
public record PriceCalculation(Guid? PromotionId,string? PromotionCode,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal Total,CalculatedLine[] Lines);
