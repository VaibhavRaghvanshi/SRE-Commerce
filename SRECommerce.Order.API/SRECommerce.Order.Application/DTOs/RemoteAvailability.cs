using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record RemoteAvailability(Guid VariantId,decimal Available,bool LowStock,bool Tracked);
