using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class CouponRedemption:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid QuoteId {get;set;} public Guid? CouponId {get;set;} public Guid? UserId {get;set;} public RedemptionStatus Status {get;set;} public DateTimeOffset CreatedAt {get;set;} public DateTimeOffset ExpiresAt {get;set;} public byte[] Version {get;set;}=[];
}
