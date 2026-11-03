using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Discovery.Domain;

namespace SRECommerce.Discovery.Application;

public interface IDiscoveryAccess
{
    Task Public(Guid tenant, CancellationToken ct);
    Task<Guid> Human(Guid tenant, ActorPermission role, CancellationToken ct);
}

public interface IDiscoverySources
{
    Task<IndexPage> CatalogPage(Guid tenant, int page, CancellationToken ct);
    Task<IndexProduct[]> PublicProducts(Guid tenant, Guid[] ids, CancellationToken ct);
    Task<bool> VerifiedPurchase(Guid tenant, Guid user, Guid product, CancellationToken ct);
}

public interface IDiscoveryStore
{
    Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<SearchDocument[]> Documents(Guid[] ids, CancellationToken ct);
    Task<SearchDocument[]> Search(string? condition, SearchQuery query, CancellationToken ct);
    Task<ProductReview?> MyReview(Guid product, Guid user, CancellationToken ct);
    Task<ProductReview?> Review(Guid id, CancellationToken ct);
    Task<ProductReview[]> Reviews(Guid? product, ReviewState state, int page, CancellationToken ct);
    void Add<T>(T entity) where T : class;
    void Version(ProductReview review, string expected);
    Task Save(string action, string resource, CancellationToken ct);
}
