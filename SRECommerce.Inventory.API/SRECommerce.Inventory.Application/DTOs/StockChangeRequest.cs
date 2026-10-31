using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Application;
public record StockChangeRequest(Guid OperationId,Guid WarehouseId,Guid VariantId,StockAction Action,decimal Quantity,string Reason,Guid? BatchId=null,string? Lot=null,DateTimeOffset? ExpiresAt=null,string? Version=null);
