using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderReservation:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid OrderId {get;set;}public Guid WarehouseId {get;set;}public string Items {get;set;}="[]";public ReservationStep Status {get;set;}public string? RemoteVersion {get;set;}public DateTimeOffset ExpiresAt {get;set;}public byte[] Version {get;set;}=[];
}
