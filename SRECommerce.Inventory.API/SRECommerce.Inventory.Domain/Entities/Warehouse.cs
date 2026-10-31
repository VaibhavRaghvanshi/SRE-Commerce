using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class Warehouse:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid BranchId {get;set;} public string Code {get;set;}="";
    public string Name {get;set;}="";
    public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
