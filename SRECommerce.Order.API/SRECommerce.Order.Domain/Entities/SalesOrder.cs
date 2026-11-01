using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class SalesOrder:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid? UserId {get;set;}public Guid? CartId {get;set;}public int? CartRevision {get;set;}public Guid? PreparationId {get;set;}public Guid? QuoteId {get;set;}public Guid? ShippingQuoteId {get;set;}
 public int Revision {get;set;}public string Number {get;set;}="";public OrderStatus Status {get;set;}public PaymentState PaymentState {get;set;}public string PaymentMethod {get;set;}="";public string Currency {get;set;}="INR";public int DecimalPlaces {get;set;}=2;
 public decimal Subtotal {get;set;}public decimal Discount {get;set;}public decimal Net {get;set;}public decimal Tax {get;set;}public decimal ItemsTotal {get;set;}public decimal ShippingTotal {get;set;}public decimal Total {get;set;}
 public string ContactDocument {get;set;}="{}";public string PriceSnapshot {get;set;}="{}";public string ShippingSnapshot {get;set;}="{}";public string? InvoiceReference {get;set;}public bool Historical {get;set;}public string? SourceReference {get;set;}
 public decimal Refunded {get;set;}public DateTimeOffset? DeliveredAt {get;set;}
 public DateTimeOffset CreatedAt {get;set;}public DateTimeOffset UpdatedAt {get;set;}public byte[] Version {get;set;}=[];public List<OrderItem> Items {get;set;}=[];
}
