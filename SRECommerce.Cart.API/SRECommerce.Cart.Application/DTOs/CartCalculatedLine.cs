namespace SRECommerce.Cart.Application;
public sealed record CartCalculatedLine(Guid VariantId,string Sku,Guid PriceId,string PriceVersion,decimal Quantity,decimal Mrp,decimal UnitPrice,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal Total,decimal TaxRate,bool TaxInclusive);
