using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class Product : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public string SeoTitle { get; set; } = "";
    public string SeoDescription { get; set; } = "";
    public ProductKind Kind { get; set; }
    public ProductStatus Status { get; set; }
    public bool Visible { get; set; } = true;
    public bool B2BOnly { get; set; }
    public string Attributes { get; set; } = "{}";
    public string Specifications { get; set; } = "{}";
    public int PublishedRevision { get; set; }
    public byte[] Version { get; set; } = [];
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<ProductVariant> Variants { get; set; } = [];
    public List<ProductMedia> Media { get; set; } = [];
    public List<ProductBundleItem> BundleItems { get; set; } = [];
    public List<ProductRelationship> Relationships { get; set; } = [];
}
