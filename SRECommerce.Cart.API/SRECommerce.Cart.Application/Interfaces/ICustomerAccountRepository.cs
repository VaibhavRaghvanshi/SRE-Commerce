using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.Application;

public interface ICustomerAccountRepository
{
    Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<CustomerAddress[]> Addresses(Guid user, CancellationToken ct);
    Task<CustomerAddress?> Address(Guid user, Guid id, CancellationToken ct);
    Task<SavedProduct[]> Saved(Guid user, SavedProductKind kind, int page, CancellationToken ct);
    Task<SavedProduct?> Saved(Guid user, SavedProductKind kind, Guid product, CancellationToken ct);
    Task<int> Count(Guid user, SavedProductKind kind, CancellationToken ct);
    Task TrimRecent(Guid user, CancellationToken ct);
    void Add<T>(T row) where T : class;
    void Version<T>(T row, string expected) where T : class, SRECommerce.BuildingBlocks.Kernel.IVersionedRecord;
    Task Save(string action, Guid resource, CancellationToken ct);
}
