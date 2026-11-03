using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class ShipmentCallback : ITenantRecord, IVersionedRecord
{
 public Guid TenantId {get;set;} public Guid Id {get;set;} public Guid ShipmentId {get;set;}
 public int Sequence {get;set;} public int TargetStatus {get;set;} public string? RequestDocument {get;set;}
 public bool Sent {get;set;} public bool ManualReview {get;set;} public int Attempts {get;set;}
 public string? LastFault {get;set;} public DateTimeOffset NextAttemptAt {get;set;}
 public Guid? LeaseId {get;set;} public DateTimeOffset? LeaseUntil {get;set;}
 public DateTimeOffset CreatedAt {get;set;} public byte[] Version {get;set;}=[];
}
