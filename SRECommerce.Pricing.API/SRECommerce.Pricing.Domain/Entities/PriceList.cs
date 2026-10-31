using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class PriceList:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Code {get;set;}="";
    public string Name {get;set;}="";
    public string Currency {get;set;}="INR";
    public PriceAudience Audience {get;set;} public Guid? CustomerGroupId {get;set;} public int Priority {get;set;} public DateTimeOffset StartsAt {get;set;} public DateTimeOffset EndsAt {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
