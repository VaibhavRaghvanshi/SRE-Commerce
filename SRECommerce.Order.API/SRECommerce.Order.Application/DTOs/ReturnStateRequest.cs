using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record ReturnStateRequest(Guid OperationId,string Version,ReturnStatus Status,Guid? WarehouseId=null,bool Resalable=false);
