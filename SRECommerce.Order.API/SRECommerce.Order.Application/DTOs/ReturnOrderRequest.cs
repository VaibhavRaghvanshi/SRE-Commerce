using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record ReturnOrderRequest(Guid Id,string Version,OrderQuantity[] Items,string Reason);
