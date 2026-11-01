using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record ReturnView(Guid Id,Guid OrderId,OrderQuantity[] Items,string Reason,ReturnStatus Status,decimal RefundAmount,Guid? WarehouseId,bool Resalable,Guid? ExchangeId,string Version);
