using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderItem:ITenantRecord
{
 public Guid TenantId {get;set;}public Guid OrderId {get;set;}public Guid VariantId {get;set;}public Guid ProductId {get;set;}public string Sku {get;set;}="";public string Name {get;set;}="";
 public decimal Quantity {get;set;}public decimal ShipmentRequested {get;set;}public decimal RefundAllocated {get;set;}public decimal Fulfilled {get;set;}public decimal Delivered {get;set;}public decimal ReturnRequested {get;set;}public decimal Returned {get;set;}
 public decimal UnitPrice {get;set;}public decimal Subtotal {get;set;}public decimal Discount {get;set;}public decimal Net {get;set;}public decimal Tax {get;set;}public decimal Total {get;set;}
}
