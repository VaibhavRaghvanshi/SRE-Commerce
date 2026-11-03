using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed record ShipmentStateRequest(Guid OperationId,string Version,ShipmentStatus Status,string Note);
