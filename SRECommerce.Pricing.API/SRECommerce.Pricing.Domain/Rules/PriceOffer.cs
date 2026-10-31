namespace SRECommerce.Pricing.Domain;
public record PriceOffer(Guid Id,string Code,PromotionKind Kind,decimal Value,int BuyQuantity,int GetQuantity,decimal MinimumSpend,decimal? MaximumDiscount,int Priority,IReadOnlySet<Guid> VariantIds);
