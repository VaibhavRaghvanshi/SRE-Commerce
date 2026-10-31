namespace SRECommerce.Catalog.Application;
public record VariantInput(Guid? Id,string Sku,Dictionary<string,string> Options,string? Barcode=null,bool Active=true);
