using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class Branch:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Code {get;set;}="";
    public string Name {get;set;}="";
    public Guid? TenantLocationId {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
