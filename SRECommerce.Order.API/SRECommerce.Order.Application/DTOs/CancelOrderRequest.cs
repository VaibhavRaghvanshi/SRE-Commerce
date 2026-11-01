using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record CancelOrderRequest(Guid OperationId,string Version,string Reason);
