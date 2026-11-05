using SRECommerce.Catalog.Domain;

namespace SRECommerce.Catalog.Application;

public interface IMediaAssetRepository
{
    Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<long> Bytes(CancellationToken ct);
    Task<MediaAsset?> Asset(Guid id, CancellationToken ct);
    Task<bool> PublishedReference(Guid tenant, string url, CancellationToken ct, bool includeB2B=false);
    Task Save(MediaAsset asset, CancellationToken ct);
}
public interface IMediaAssetStorage
{
    Task Put(Guid tenant, Guid id, ReadOnlyMemory<byte> bytes, CancellationToken ct);
    Task<Stream?> Read(Guid tenant, Guid id, CancellationToken ct);
}
