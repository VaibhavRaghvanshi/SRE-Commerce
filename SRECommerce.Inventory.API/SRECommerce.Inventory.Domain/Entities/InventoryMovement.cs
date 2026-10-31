using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class InventoryMovement:ITenantRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid OperationId {get;set;} public Guid BalanceId {get;set;} public StockAction Action {get;set;} public decimal OnHandDelta {get;set;} public decimal ReservedDelta {get;set;} public decimal DamagedDelta {get;set;} public string Reason {get;set;}="";
    public DateTimeOffset OccurredAt {get;set;}
}
