using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Cart.Domain;
public sealed class CartSession:ITenantRecord
{
 public Guid TenantId {get;set;}public Guid CartId {get;set;}public byte[] CapabilityHash {get;set;}=[];
 public DateTimeOffset ExpiresAt {get;set;}public DateTimeOffset? RevokedAt {get;set;}
}
