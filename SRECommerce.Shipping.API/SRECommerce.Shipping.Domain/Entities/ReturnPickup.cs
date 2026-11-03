using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class ReturnPickup : ITenantRecord, IVersionedRecord
{
 public Guid TenantId {get;set;} public Guid Id {get;set;} public Guid ReturnId {get;set;} public Guid OrderId {get;set;} public Guid WarehouseId {get;set;}
 public string ContactDocument {get;set;}="{}"; public string Items {get;set;}="[]";
 public DateTimeOffset WindowFrom {get;set;} public DateTimeOffset WindowTo {get;set;}
 public PickupStatus Status {get;set;} public string Note {get;set;}="";
 public DateTimeOffset CreatedAt {get;set;} public byte[] Version {get;set;}=[];
}
