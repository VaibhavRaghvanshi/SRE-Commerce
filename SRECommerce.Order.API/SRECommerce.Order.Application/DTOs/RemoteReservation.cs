using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record RemoteReservation(Guid Id,Guid WarehouseId,string Reference,int Status,DateTimeOffset ExpiresAt,RemoteReservationItem[] Items,string Version);
