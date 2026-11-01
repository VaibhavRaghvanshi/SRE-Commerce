using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotProduct(Guid Id,string Name,string Slug,bool B2BOnly,SnapshotSku[] Variants);
