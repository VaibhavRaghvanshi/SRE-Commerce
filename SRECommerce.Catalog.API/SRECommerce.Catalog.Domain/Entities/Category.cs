using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class Category : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ParentId { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public bool Archived { get; set; }
    public int BindingRevision { get; set; }
    public byte[] Version { get; set; } = [];
}
