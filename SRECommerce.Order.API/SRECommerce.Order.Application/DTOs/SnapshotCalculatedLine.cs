using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotCalculatedLine(Guid VariantId,string Sku,Guid PriceId,string PriceVersion,decimal Quantity,decimal Mrp,decimal UnitPrice,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal Total,decimal TaxRate,bool TaxInclusive);
