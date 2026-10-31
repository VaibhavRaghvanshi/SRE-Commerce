using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class CollectionProduct : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid CollectionId { get; set; }
    public Guid ProductId { get; set; }
    public int Order { get; set; }
    public bool Active { get; set; } = true;
}
