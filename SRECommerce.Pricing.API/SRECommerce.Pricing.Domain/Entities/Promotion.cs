using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class Promotion:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public string Code {get;set;}="";
    public string Name {get;set;}="";
    public PromotionKind Kind {get;set;} public decimal Value {get;set;} public int BuyQuantity {get;set;} public int GetQuantity {get;set;} public decimal MinimumSpend {get;set;} public decimal? MaximumDiscount {get;set;} public string VariantIds {get;set;}="[]";
    public Guid? CustomerGroupId {get;set;} public bool B2BOnly {get;set;} public bool Automatic {get;set;}=true;
    public int Priority {get;set;} public DateTimeOffset StartsAt {get;set;} public DateTimeOffset EndsAt {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
