using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Application;
public sealed record ReturnPickupStateRequest(Guid OperationId,string Version,PickupStatus Status,string Note);
