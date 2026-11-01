using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record OrderStateRequest(Guid OperationId,string Version,OrderStatus Status,string Reason);
