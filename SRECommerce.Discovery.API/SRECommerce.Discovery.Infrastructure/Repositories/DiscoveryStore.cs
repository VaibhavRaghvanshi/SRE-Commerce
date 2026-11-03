using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Discovery.Application;
using SRECommerce.Discovery.Domain;

namespace SRECommerce.Discovery.Infrastructure;

public sealed class DiscoveryStore(DiscoveryDbContext db, ModuleScope scope, IHttpContextAccessor contexts) : IDiscoveryStore
{
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await action(ct); await transaction.CommitAsync(ct); return result;
    }
    public Task<SearchDocument[]> Documents(Guid[] ids, CancellationToken ct) => db.Documents.Where(x => ids.Contains(x.ProductId)).ToArrayAsync(ct);
    public Task<SearchDocument[]> Search(string? condition, SearchQuery query, CancellationToken ct)
    {
        var tenant = scope.TenantId ?? throw new BusinessFault("MissingSearchTenant", 403);
        var sql = "SELECT d.* FROM dbo.SearchDocuments AS d ";
        var parameters = new List<SqlParameter>
        {
            new("@tenant", SqlDbType.UniqueIdentifier) { Value = tenant },
            new("@category", SqlDbType.UniqueIdentifier) { Value = (object?)query.Category ?? DBNull.Value },
            new("@brand", SqlDbType.UniqueIdentifier) { Value = (object?)query.Brand ?? DBNull.Value },
            new("@attribute", SqlDbType.NVarChar, 64) { Value = (object?)query.Attribute ?? DBNull.Value },
            new("@value", SqlDbType.NVarChar, 120) { Value = (object?)query.Value ?? DBNull.Value },
            new("@offset", SqlDbType.Int) { Value = (query.Page - 1) * 24 }
        };
        if (condition is not null)
        {
            sql += "INNER JOIN CONTAINSTABLE(dbo.SearchDocuments, SearchText, @condition) AS ranked ON ranked.[KEY]=d.Id ";
            parameters.Add(new("@condition", SqlDbType.NVarChar, 400) { Value = condition });
        }
        sql += """
            WHERE d.TenantId=@tenant AND (@category IS NULL OR d.CategoryId=@category)
            AND (@brand IS NULL OR d.BrandId=@brand)
            AND (@attribute IS NULL OR EXISTS (SELECT 1 FROM OPENJSON(d.Attributes) a WHERE a.[key]=@attribute AND a.[value]=@value))
            ORDER BY
            """;
        sql += query.Sort switch
        {
            "recent" => " d.IndexedAt DESC, d.Name, d.ProductId ",
            "relevance" when condition is not null => " ranked.[RANK] DESC, d.Name, d.ProductId ",
            _ => " d.Name, d.ProductId "
        };
        sql += "OFFSET @offset ROWS FETCH NEXT 25 ROWS ONLY";
        return db.Documents.FromSqlRaw(sql, parameters.Cast<object>().ToArray()).IgnoreQueryFilters().AsNoTracking().ToArrayAsync(ct);
    }
    public Task<ProductReview?> MyReview(Guid product, Guid user, CancellationToken ct) => db.Reviews.SingleOrDefaultAsync(x => x.ProductId == product && x.UserId == user, ct);
    public Task<ProductReview?> Review(Guid id, CancellationToken ct) => db.Reviews.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ProductReview[]> Reviews(Guid? product, ReviewState state, int page, CancellationToken ct)
    {
        var query = db.Reviews.AsNoTracking().Where(x => x.State == state);
        if (product is {} id) query = query.Where(x => x.ProductId == id);
        return query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip((page - 1) * 24).Take(24).ToArrayAsync(ct);
    }
    public void Add<T>(T entity) where T : class => db.Add(entity);
    public void Version(ProductReview review, string expected) => db.ExpectedVersion(review, expected);
    public async Task Save(string action, string resource, CancellationToken ct)
    { db.Record(scope, contexts, action, resource); await db.SaveChangesAsync(ct); }
}
