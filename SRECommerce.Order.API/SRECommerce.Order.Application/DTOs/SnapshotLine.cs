using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotLine(Guid ProductId,Guid VariantId,string Sku,string Name,string Slug,decimal Quantity);
