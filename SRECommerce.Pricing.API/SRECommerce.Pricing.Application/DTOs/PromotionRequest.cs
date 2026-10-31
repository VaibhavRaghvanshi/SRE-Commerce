using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public record PromotionRequest(string Code,string Name,PromotionKind Kind,decimal Value,int BuyQuantity,int GetQuantity,decimal MinimumSpend,decimal? MaximumDiscount,Guid[] VariantIds,Guid? CustomerGroupId,bool B2BOnly,bool Automatic,int Priority,DateTimeOffset StartsAt,DateTimeOffset EndsAt,bool Active=true,string? Version=null);
