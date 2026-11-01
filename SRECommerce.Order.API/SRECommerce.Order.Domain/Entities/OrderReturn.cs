using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderReturn:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid OrderId {get;set;}public string Items {get;set;}="[]";public string Reason {get;set;}="";public ReturnStatus Status {get;set;}public decimal RefundAmount {get;set;}public Guid? WarehouseId {get;set;}public bool Resalable {get;set;}public bool ReceiptRecorded {get;set;}public Guid? ExchangeId {get;set;}public DateTimeOffset CreatedAt {get;set;}public int Attempts {get;set;}public DateTimeOffset NextAttemptAt {get;set;}public string? LastFault {get;set;}public byte[] Version {get;set;}=[];
}
