using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class Supplier:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Code {get;set;}="";
    public string Name {get;set;}="";
    public string? ContactEmail {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
