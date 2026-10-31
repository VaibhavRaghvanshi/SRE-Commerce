using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class ProductRelationship : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public Guid RelatedProductId { get; set; }
    public string Kind { get; set; } = "related";
    public bool Active { get; set; } = true;
}
