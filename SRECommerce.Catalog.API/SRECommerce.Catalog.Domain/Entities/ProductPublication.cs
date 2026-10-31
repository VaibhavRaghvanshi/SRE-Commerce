using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class ProductPublication : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid ProductId { get; set; }
    public int Revision { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public bool Visible { get; set; }
    public bool B2BOnly { get; set; }
    public string Document { get; set; } = "";
    public string Hash { get; set; } = "";
    public DateTimeOffset PublishedAt { get; set; } = DateTimeOffset.UtcNow;
}
