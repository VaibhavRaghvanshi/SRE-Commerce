using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class ProductBundleItem : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid ComponentVariantId { get; set; }
    public decimal Quantity { get; set; }
    public bool Active { get; set; } = true;
}
