using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class Shipment : ITenantRecord, IVersionedRecord
{
 public Guid TenantId {get;set;} public Guid Id {get;set;} public Guid OrderId {get;set;}
 public Guid QuoteId {get;set;} public Guid WarehouseId {get;set;} public Guid? UserId {get;set;}
 public DeliveryKind Kind {get;set;} public string Provider {get;set;}="";
 public string CourierReference {get;set;}=""; public string ContactDocument {get;set;}="{}";
 public string ProofHash {get;set;}=""; public ShipmentStatus Status {get;set;}
 public int Revision {get;set;} public DateTimeOffset CreatedAt {get;set;} public DateTimeOffset UpdatedAt {get;set;}
 public DateTimeOffset? DispatchedAt {get;set;} public DateTimeOffset? DeliveredAt {get;set;}
 public byte[] Version {get;set;}=[]; public List<ShipmentItem> Items {get;set;}=[];
}
