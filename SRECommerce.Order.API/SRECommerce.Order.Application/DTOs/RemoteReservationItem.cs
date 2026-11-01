using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record RemoteReservationItem(Guid VariantId,Guid BalanceId,decimal Quantity);
