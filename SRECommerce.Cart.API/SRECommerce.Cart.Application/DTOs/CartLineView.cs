namespace SRECommerce.Cart.Application;
public sealed record CartLineView(Guid ProductId,Guid VariantId,string Sku,string Name,string Slug,decimal Quantity);
