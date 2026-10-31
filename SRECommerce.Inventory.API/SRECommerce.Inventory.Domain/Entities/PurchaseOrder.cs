using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class PurchaseOrder:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public int ReceiptRevision {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid SupplierId {get;set;} public Guid WarehouseId {get;set;} public string Reference {get;set;}="";
    public string Currency {get;set;}="INR";
    public PurchaseStatus Status {get;set;} public DateTimeOffset CreatedAt {get;set;} public byte[] Version {get;set;}=[];
    public List<PurchaseOrderLine> Lines {get;set;}=[];
}
