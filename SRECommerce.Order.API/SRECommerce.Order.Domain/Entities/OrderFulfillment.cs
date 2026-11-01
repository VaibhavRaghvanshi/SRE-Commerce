using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderFulfillment:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid OrderId {get;set;}public Guid WarehouseId {get;set;}public string Items {get;set;}="[]";public FulfillmentStatus Status {get;set;}public string? CourierReference {get;set;}public DateTimeOffset CreatedAt {get;set;}public int Attempts {get;set;}public DateTimeOffset NextAttemptAt {get;set;}public string? LastFault {get;set;}public byte[] Version {get;set;}=[];
}
