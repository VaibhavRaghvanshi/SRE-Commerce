using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Cart.Domain;
public sealed class CartCheckoutClaim:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid CartId {get;set;}public Guid PreparationId {get;set;}public Guid? UserId {get;set;}
    public int CartRevision {get;set;}public CartClaimStatus Status {get;set;}public DateTimeOffset CreatedAt {get;set;}public DateTimeOffset ExpiresAt {get;set;}
    public byte[] Version {get;set;}=[];
}
