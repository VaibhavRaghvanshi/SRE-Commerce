using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Cart.Application;
using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.Infrastructure;

public sealed class CustomerAccountRepository(CartDbContext db, ModuleScope scope, IHttpContextAccessor contexts) : ICustomerAccountRepository
{
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var result = await action(ct); await tx.CommitAsync(ct); return result;
    }
    public Task<CustomerAddress[]> Addresses(Guid user, CancellationToken ct) => db.Addresses.Where(row => row.UserId == user && row.Active)
        .OrderByDescending(row => row.UpdatedAt).ThenBy(row => row.Id).ToArrayAsync(ct);
    public Task<CustomerAddress?> Address(Guid user, Guid id, CancellationToken ct) => db.Addresses.SingleOrDefaultAsync(row => row.UserId == user && row.Id == id, ct);
    public Task<SavedProduct[]> Saved(Guid user, SavedProductKind kind, int page, CancellationToken ct) => db.SavedProducts
        .Where(row => row.UserId == user && row.Kind == kind && row.Active).OrderByDescending(row => row.UpdatedAt).ThenBy(row => row.ProductId)
        .Skip((page - 1) * 25).Take(26).ToArrayAsync(ct);
    public Task<SavedProduct?> Saved(Guid user, SavedProductKind kind, Guid product, CancellationToken ct) =>
        db.SavedProducts.SingleOrDefaultAsync(row => row.UserId == user && row.Kind == kind && row.ProductId == product, ct);
    public Task<int> Count(Guid user, SavedProductKind kind, CancellationToken ct) =>
        db.SavedProducts.CountAsync(row => row.UserId == user && row.Kind == kind && row.Active, ct);
    public async Task TrimRecent(Guid user, CancellationToken ct)
    {
        var excess = await db.SavedProducts.Where(row => row.UserId == user && row.Kind == SavedProductKind.RecentlyViewed && row.Active)
            .OrderByDescending(row => row.UpdatedAt).ThenBy(row => row.ProductId).Skip(25).ToArrayAsync(ct);
        foreach (var row in excess) row.Active = false;
    }
    public void Add<T>(T row) where T : class => db.Add(row);
    public void Version<T>(T row, string expected) where T : class, SRECommerce.BuildingBlocks.Kernel.IVersionedRecord => db.ExpectedVersion(row, expected);
    public async Task Save(string action, Guid resource, CancellationToken ct)
    {
        db.Record(scope, contexts, action, resource.ToString()); await db.SaveChangesAsync(ct);
    }
}
