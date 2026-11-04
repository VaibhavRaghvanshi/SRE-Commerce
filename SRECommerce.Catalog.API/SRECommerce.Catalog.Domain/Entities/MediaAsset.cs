using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Catalog.Domain;

public sealed class MediaAsset : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string ContentType { get; set; } = "";
    public string Hash { get; set; } = "";
    public int Length { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
