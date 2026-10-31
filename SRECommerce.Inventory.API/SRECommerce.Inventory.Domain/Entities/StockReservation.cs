using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class StockReservation:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid WarehouseId {get;set;} public string Reference {get;set;}="";
    public string PayloadHash {get;set;}="";
    public ReservationStatus Status {get;set;} public DateTimeOffset ExpiresAt {get;set;} public DateTimeOffset CreatedAt {get;set;} public byte[] Version {get;set;}=[];
    public List<ReservationLine> Lines {get;set;}=[];
}
