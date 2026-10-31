using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class CategoryAttribute : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid CategoryId { get; set; }
    public Guid AttributeId { get; set; }
    public bool Required { get; set; }
    public bool Active { get; set; } = true;
}
