using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class PurchaseOrderLine:ITenantRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid PurchaseOrderId {get;set;} public Guid VariantId {get;set;} public decimal Ordered {get;set;} public decimal Received {get;set;} public decimal UnitCost {get;set;}
}
