namespace SRECommerce.Pricing.Application;
public record PriceRequest(Guid PriceListId,Guid ProductId,Guid VariantId,Guid TaxPolicyId,decimal MinimumQuantity,decimal Mrp,decimal SellingPrice,decimal? CostPrice,bool Active=true,string? Version=null);
