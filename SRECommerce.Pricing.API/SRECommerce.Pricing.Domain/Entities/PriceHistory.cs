using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class PriceHistory:ITenantRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid PriceId {get;set;} public string Before {get;set;}="null";
    public string After {get;set;}="";
    public DateTimeOffset ChangedAt {get;set;}
}
