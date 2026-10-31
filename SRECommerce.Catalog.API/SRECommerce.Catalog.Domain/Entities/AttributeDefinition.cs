using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public sealed class AttributeDefinition : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AttributeDataType DataType { get; set; }
    public string Options { get; set; } = "[]";
    public string? Unit { get; set; }
    public bool Archived { get; set; }
    public byte[] Version { get; set; } = [];
}
