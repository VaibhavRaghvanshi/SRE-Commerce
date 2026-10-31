using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class StockItem:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid ProductId {get;set;} public string Sku {get;set;}="";
    public string? Barcode {get;set;} public bool Active {get;set;}=true;
    public decimal ReorderLevel {get;set;} public byte[] Version {get;set;}=[];
}
