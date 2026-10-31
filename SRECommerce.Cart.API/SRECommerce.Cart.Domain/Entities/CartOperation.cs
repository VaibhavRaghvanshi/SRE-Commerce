using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Cart.Domain;
public sealed class CartOperation:ITenantRecord
{
 public Guid TenantId {get;set;}public Guid CartId {get;set;}public Guid Id {get;set;}
 public string Hash {get;set;}="";public string Result {get;set;}="";public DateTimeOffset CreatedAt {get;set;}
}
