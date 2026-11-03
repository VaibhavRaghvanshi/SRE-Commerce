using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class TrackingEvent : ITenantRecord
{
 public Guid TenantId {get;set;} public Guid Id {get;set;}=Guid.NewGuid();
 public Guid ShipmentId {get;set;} public int Sequence {get;set;} public ShipmentStatus Status {get;set;}
 public string Note {get;set;}=""; public DateTimeOffset OccurredAt {get;set;}
}
