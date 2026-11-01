using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record FulfillOrderRequest(Guid Id,string Version,Guid WarehouseId,OrderQuantity[] Items);
