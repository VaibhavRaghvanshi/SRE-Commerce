using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderExchange:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid OrderId {get;set;}public Guid ReturnId {get;set;}public Guid OriginalVariantId {get;set;}public Guid ReplacementVariantId {get;set;}public decimal Quantity {get;set;}public ExchangeStatus Status {get;set;}public Guid? ReplacementOrderId {get;set;}public DateTimeOffset CreatedAt {get;set;}public byte[] Version {get;set;}=[];
}
