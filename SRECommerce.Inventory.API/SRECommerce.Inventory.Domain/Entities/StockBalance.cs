using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class StockBalance:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid WarehouseId {get;set;} public Guid VariantId {get;set;} public Guid BatchId {get;set;} public decimal OnHand {get;set;} public decimal Reserved {get;set;} public decimal Damaged {get;set;} public byte[] Version {get;set;}=[];
}
