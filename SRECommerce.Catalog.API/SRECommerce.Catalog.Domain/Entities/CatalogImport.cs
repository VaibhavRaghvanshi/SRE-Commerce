using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class CatalogImport : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string Source { get; set; } = "";
    public string Hash { get; set; } = "";
    public string Result { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
