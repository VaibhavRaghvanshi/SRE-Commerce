using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Catalog.Domain;
namespace SRECommerce.Catalog.Application;
public interface ICatalogRepository
{
    Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct);
    Task<IReadOnlyCollection<Category>> Categories(CancellationToken ct);
    Task<IReadOnlyCollection<Brand>> Brands(CancellationToken ct);
    Task<IReadOnlyCollection<AttributeDefinition>> Definitions(CancellationToken ct);
    Task<IReadOnlyCollection<CategoryAttribute>> Bindings(Guid category,CancellationToken ct);
    Task<Product?> Product(Guid id,CancellationToken ct);
    Task<ProductVariant?> Variant(Guid id,CancellationToken ct);
    Task<CatalogPage<Product>> Drafts(CatalogQuery query,CancellationToken ct);
    Task<CatalogPage<ProductView>> Published(CatalogQuery query,CancellationToken ct);
    Task<ProductView?> PublishedProduct(string slug,CancellationToken ct);
    Task<ProductView?> PublishedVariant(Guid id,CancellationToken ct);
    Task<IReadOnlyCollection<ProductCollection>> Collections(CancellationToken ct);
    Task<CatalogImport?> Import(Guid id,CancellationToken ct);
    void Add<T>(T entity)where T:class;
    void Version(IVersionedRecord entity,string? expected);
    Task Commit(string action,Guid resource,object? publishedEvent,CancellationToken ct);
}
