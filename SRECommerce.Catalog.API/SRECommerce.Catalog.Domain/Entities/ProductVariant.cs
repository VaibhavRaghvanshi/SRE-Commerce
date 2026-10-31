using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class ProductVariant : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = "";
    public string? Barcode { get; set; }
    public string Options { get; set; } = "{}";
    public bool Active { get; set; } = true;
}
