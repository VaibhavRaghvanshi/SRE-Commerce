using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Cart.Domain;
public sealed class CheckoutPreparation:ITenantRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid CartId {get;set;}public Guid? UserId {get;set;}
 public int CartRevision {get;set;}public Guid QuoteId {get;set;}public string Hash {get;set;}="";public string Document {get;set;}="";
 public DateTimeOffset CreatedAt {get;set;}public DateTimeOffset ExpiresAt {get;set;}
}
