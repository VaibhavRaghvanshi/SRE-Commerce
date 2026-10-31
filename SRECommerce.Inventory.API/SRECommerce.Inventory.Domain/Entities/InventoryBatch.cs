using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class InventoryBatch:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid WarehouseId {get;set;} public Guid VariantId {get;set;} public string Lot {get;set;}="";
    public DateTimeOffset? ExpiresAt {get;set;} public DateTimeOffset ReceivedAt {get;set;}=DateTimeOffset.UtcNow;
    public byte[] Version {get;set;}=[];
}
