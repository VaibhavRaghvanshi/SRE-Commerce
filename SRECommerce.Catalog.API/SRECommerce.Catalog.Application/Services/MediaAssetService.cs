using System.Security.Cryptography;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Catalog.Domain;

namespace SRECommerce.Catalog.Application;

public sealed class MediaAssetService(IMediaAssetRepository assets, IMediaAssetStorage storage, ICatalogAccess access)
{
    private const long MaximumTenantBytes = 268435456;
    private static string Url(Guid tenant, Guid id) => $"/commerce/catalog/public/tenants/{tenant:D}/catalog/media/{id:D}";
    public async Task<MediaAssetView> Upload(Guid tenant, byte[] content, CancellationToken ct)
    {
        await access.Authorize(tenant, ActorPermission.Manager, ct);
        var image = ProductImageRules.Inspect(content);
        if (await assets.Bytes(ct) + content.Length > MaximumTenantBytes) throw new BusinessFault("MediaStorageQuotaReached");
        var id = Guid.NewGuid();
        await storage.Put(tenant, id, content, ct);
        return await assets.Transaction(async token =>
        {
            if (await assets.Bytes(token) + content.Length > MaximumTenantBytes) throw new BusinessFault("MediaStorageQuotaReached");
            var asset = new MediaAsset { TenantId = tenant, Id = id, ContentType = image.ContentType,
                Hash = Convert.ToHexString(SHA256.HashData(content)), Length = content.Length, Width = image.Width, Height = image.Height, CreatedAt = DateTimeOffset.UtcNow };
            await assets.Save(asset, token);
            return new MediaAssetView(id, Url(tenant, id), image.ContentType, content.Length, image.Width, image.Height);
        }, ct);
    }
    public async Task<MediaAssetContent> Public(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Public(tenant, ct);
        var asset = await assets.Asset(id, ct);
        if (asset is null || !await assets.PublishedReference(tenant, Url(tenant, id), ct)) throw new BusinessFault("MediaNotFound", 404);
        var stream = await storage.Read(tenant, id, ct) ?? throw new BusinessFault("MediaNotFound", 404);
        return new(stream, asset.ContentType);
    }
    public async Task<MediaAssetContent> Buyer(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Authorize(tenant, ActorPermission.Customer, ct);
        var asset = await assets.Asset(id, ct);
        if (asset is null || !await assets.PublishedReference(tenant, Url(tenant, id), ct, access.CanViewB2B)) throw new BusinessFault("MediaNotFound", 404);
        var stream = await storage.Read(tenant, id, ct) ?? throw new BusinessFault("MediaNotFound", 404);
        return new(stream, asset.ContentType);
    }
}
