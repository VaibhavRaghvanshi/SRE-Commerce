using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Cart.Domain;
public sealed class CartItem:ITenantRecord
{
 public Guid TenantId {get;set;}public Guid CartId {get;set;}public Guid VariantId {get;set;}public Guid ProductId {get;set;}
 public string Sku {get;set;}="";public string Name {get;set;}="";public string Slug {get;set;}="";
 public decimal Quantity {get;set;}public bool Active {get;set;}=true;
}
