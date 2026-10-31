namespace SRECommerce.Pricing.Domain;
public record PriceInput(Guid VariantId,string Sku,Guid PriceId,string PriceVersion,decimal Quantity,decimal Mrp,decimal SellingPrice,decimal TaxRate,bool TaxInclusive,bool IsBundle);
