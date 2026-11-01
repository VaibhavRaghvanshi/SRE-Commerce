namespace SRECommerce.Inventory.Application;
public sealed record CustomerReturnReceipt(Guid OperationId,Guid OrderId,Guid WarehouseId,CustomerReturnLine[] Items,bool Resalable=false);
