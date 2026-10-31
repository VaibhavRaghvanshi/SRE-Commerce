using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class StockOperation:ITenantRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public StockAction Action {get;set;} public string PayloadHash {get;set;}="";
    public string Result {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;}
}
