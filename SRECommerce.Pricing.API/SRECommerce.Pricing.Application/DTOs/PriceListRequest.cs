using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public record PriceListRequest(string Code,string Name,string Currency,PriceAudience Audience,Guid? CustomerGroupId,int Priority,DateTimeOffset StartsAt,DateTimeOffset EndsAt,bool Active=true,string? Version=null);
