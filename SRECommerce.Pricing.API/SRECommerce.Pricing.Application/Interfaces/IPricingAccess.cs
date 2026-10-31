using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Pricing.Application;
public interface IPricingAccess
{
    UserAccess Actor {get;}
    Task Authorize(Guid tenant,ActorPermission permission,CancellationToken ct);
    Task<PublicTenant> Tenant(Guid tenant,CancellationToken ct);
    Task<PublicTenant> Guest(Guid tenant,CancellationToken ct);
    Task<CatalogPriceItem> CatalogItem(Guid tenant,Guid product,Guid variant,CancellationToken ct);
    Task<PublishedSku[]> CatalogItems(Guid tenant,Guid[] variants,CancellationToken ct);
    Task<Guid[]> VisibleVariants(Guid tenant,Guid[] ids,bool publicRead,CancellationToken ct);
    Task CustomerMember(Guid tenant,Guid user,CancellationToken ct);
    Task Machine(Guid tenant,string[] sources,CancellationToken ct);
}
