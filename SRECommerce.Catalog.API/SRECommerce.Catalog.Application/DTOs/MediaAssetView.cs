namespace SRECommerce.Catalog.Application;

public sealed record MediaAssetView(Guid Id, string Url, string ContentType, int Length, int Width, int Height);
public sealed record MediaAssetContent(Stream Stream, string ContentType);
