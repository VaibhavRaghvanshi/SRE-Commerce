namespace SRECommerce.Pricing.Application;
public record ProductPriceView(Guid Id,Guid PriceListId,Guid ProductId,Guid VariantId,string Sku,bool IsBundle,Guid TaxPolicyId,decimal MinimumQuantity,decimal Mrp,decimal SellingPrice,decimal? CostPrice,bool Active,string Version);
