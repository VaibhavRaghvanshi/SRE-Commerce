using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class ProductMedia : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public string Url { get; set; } = "";
    public string Alt { get; set; } = "";
    public int Order { get; set; }
    public bool Active { get; set; } = true;
}
