using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class PricingQuote:ITenantRecord
{
    public Guid TenantId {get;set;}
    public Guid? CustomerGroupId {get;set;}
    public string GroupContextHash {get;set;}="";
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid? UserId {get;set;} public Guid? CouponId {get;set;} public string RequestHash {get;set;}="";
    public string Document {get;set;}="";
    public DateTimeOffset CreatedAt {get;set;} public DateTimeOffset ExpiresAt {get;set;}
}
