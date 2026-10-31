namespace SRECommerce.Inventory.Application;
public record PurchaseRequest(Guid SupplierId,Guid WarehouseId,string Reference,string Currency,PurchaseInput[] Lines);
public record PurchaseInput(Guid VariantId,decimal Quantity,decimal UnitCost);
