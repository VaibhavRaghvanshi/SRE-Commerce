using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record OrderQuantity(Guid VariantId,decimal Quantity);
