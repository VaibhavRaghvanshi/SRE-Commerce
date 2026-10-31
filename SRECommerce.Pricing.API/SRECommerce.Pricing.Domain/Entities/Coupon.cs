using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class Coupon:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid PromotionId {get;set;} public string Code {get;set;}="";
    public int GlobalLimit {get;set;} public int PerCustomerLimit {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
