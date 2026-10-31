namespace SRECommerce.Cart.Application;
public sealed record CartCatalogProduct(Guid Id,string Name,string Slug,CartCatalogVariant[] Variants);
