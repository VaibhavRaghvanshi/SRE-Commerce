using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class PricingSettings:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}
    public string Currency {get;set;}="INR";
    public int DecimalPlaces {get;set;}=2;
    public byte[] Version {get;set;}=[];
}
