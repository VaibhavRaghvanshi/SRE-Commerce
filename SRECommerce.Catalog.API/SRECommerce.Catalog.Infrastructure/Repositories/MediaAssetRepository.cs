using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Domain;

namespace SRECommerce.Catalog.Infrastructure;

public sealed class MediaAssetRepository(CatalogDbContext db, ModuleScope scope, IHttpContextAccessor contexts) : IMediaAssetRepository
{
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var result = await action(ct); await tx.CommitAsync(ct); return result;
    }
    public async Task<long> Bytes(CancellationToken ct) => await db.Assets.SumAsync(row => (long?)row.Length, ct) ?? 0;
    public Task<MediaAsset?> Asset(Guid id, CancellationToken ct) => db.Assets.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct);
    public Task<bool> PublishedReference(Guid tenant, string url, CancellationToken ct, bool includeB2B=false) => db.Database.SqlQuery<bool>($"""
        SELECT CAST(CASE WHEN EXISTS (
          SELECT 1 FROM ProductPublications pp INNER JOIN Products p ON p.TenantId=pp.TenantId AND p.Id=pp.ProductId AND p.PublishedRevision=pp.Revision
          WHERE pp.TenantId={tenant} AND pp.Visible=1 AND ({includeB2B}=1 OR pp.B2BOnly=0) AND p.Status<>2
          AND EXISTS (SELECT 1 FROM Categories category WHERE category.TenantId=pp.TenantId AND category.Id=pp.CategoryId AND category.Archived=0)
          AND (pp.BrandId IS NULL OR EXISTS (SELECT 1 FROM Brands brand WHERE brand.TenantId=pp.TenantId AND brand.Id=pp.BrandId AND brand.Archived=0))
          AND EXISTS (
            SELECT 1 FROM OPENJSON(pp.Document,'$.media') WITH (Url nvarchar(1024) '$.url') image WHERE image.Url={url}
          )
        ) THEN 1 ELSE 0 END AS bit) AS [Value]
        """).SingleAsync(ct);
    public async Task Save(MediaAsset asset, CancellationToken ct)
    {
        db.Assets.Add(asset); db.Record(scope, contexts, "catalog.media.uploaded", asset.Id.ToString()); await db.SaveChangesAsync(ct);
    }
}
