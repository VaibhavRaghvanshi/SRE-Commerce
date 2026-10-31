namespace SRECommerce.Pricing.Application;
public record CatalogPriceItem(Guid ProductId,Guid VariantId,string Sku,bool IsBundle);
