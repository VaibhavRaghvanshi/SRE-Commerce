using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record FulfillmentStateRequest(Guid OperationId,string Version,FulfillmentStatus Status,string? CourierReference=null);
