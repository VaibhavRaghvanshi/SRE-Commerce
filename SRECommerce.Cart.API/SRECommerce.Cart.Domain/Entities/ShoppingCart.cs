using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Cart.Domain;
public sealed class ShoppingCart:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid? UserId {get;set;}
 public CartStatus Status {get;set;}public Guid? MergedIntoId {get;set;}public int Revision {get;set;}
 public DateTimeOffset CreatedAt {get;set;}public DateTimeOffset UpdatedAt {get;set;}public DateTimeOffset ExpiresAt {get;set;}
 public byte[] Version {get;set;}=[];public List<CartItem> Items {get;set;}=[];
}
