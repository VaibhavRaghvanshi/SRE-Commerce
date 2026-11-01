using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class CheckoutIntent:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid PreparationId {get;set;}public Guid ShippingQuoteId {get;set;}public Guid? UserId {get;set;}public string? CustomerRole {get;set;}
 public byte[]? GuestKeyHash {get;set;}public string RequestHash {get;set;}="";public string RequestDocument {get;set;}="";public CheckoutState State {get;set;}public SagaPhase Phase {get;set;}
 public DateTimeOffset CreatedAt {get;set;}public DateTimeOffset Deadline {get;set;}public DateTimeOffset NextAttemptAt {get;set;}public int Attempts {get;set;}public string? LastFault {get;set;}
 public string CartClaimDocument {get;set;}="{}";public string? CancellationReason {get;set;}
 public Guid? LeaseId {get;set;}public DateTimeOffset? LeaseUntil {get;set;}public byte[] Version {get;set;}=[];
}
