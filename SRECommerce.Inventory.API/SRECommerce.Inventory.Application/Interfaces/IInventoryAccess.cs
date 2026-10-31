using SRECommerce.BuildingBlocks.Contracts;
namespace SRECommerce.Inventory.Application;
public interface IInventoryAccess
{
    Task Authorize(Guid tenant,ActorPermission permission,CancellationToken ct);
    Task Public(Guid tenant,CancellationToken ct);
    Task<Guid[]> PublicVariants(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<Guid[]> ShoppingVariants(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<CatalogStockItem> CatalogItem(Guid tenant,Guid product,Guid variant,CancellationToken ct);
    Task<PublishedSku[]> CatalogItems(Guid tenant,Guid[] variants,CancellationToken ct);
    Task Machine(Guid tenant,string[] sources,CancellationToken ct);
}
