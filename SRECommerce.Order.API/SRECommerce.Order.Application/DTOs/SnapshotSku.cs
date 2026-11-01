using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotSku(Guid Id,string Sku,bool Active);
