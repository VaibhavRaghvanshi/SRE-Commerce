using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class TaxPolicy:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Code {get;set;}="";
    public string Name {get;set;}="";
    public decimal Rate {get;set;} public bool Inclusive {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
