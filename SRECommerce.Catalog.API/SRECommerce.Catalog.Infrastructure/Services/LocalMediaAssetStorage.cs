using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Catalog.Application;

namespace SRECommerce.Catalog.Infrastructure;

public sealed class LocalMediaAssetStorage(IConfiguration configuration, IHostEnvironment environment) : IMediaAssetStorage
{
    private string PathFor(Guid tenant, Guid id)
    {
        if (!environment.IsDevelopment()) throw new BusinessFault("ProductionMediaStorageNotConfigured", 503);
        var root = configuration["Media:Root"] ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SRECommerce", "Media", "Catalog");
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("Absolute media storage root required");
        return Path.Combine(root, tenant.ToString("N"), id.ToString("N") + ".image");
    }
    public async Task Put(Guid tenant, Guid id, ReadOnlyMemory<byte> bytes, CancellationToken ct)
    {
        var path = PathFor(tenant, id); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await stream.WriteAsync(bytes, ct); await stream.FlushAsync(ct);
    }
    public Task<Stream?> Read(Guid tenant, Guid id, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested(); var path = PathFor(tenant, id);
        try { return Task.FromResult<Stream?>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous)); }
        catch (FileNotFoundException) { return Task.FromResult<Stream?>(null); }
        catch (DirectoryNotFoundException) { return Task.FromResult<Stream?>(null); }
    }
}
