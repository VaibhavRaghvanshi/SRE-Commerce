using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class ShipmentItem : ITenantRecord
{
 public Guid TenantId {get;set;} public Guid ShipmentId {get;set;} public Guid VariantId {get;set;}
 public decimal Quantity {get;set;}
}
