namespace SRECommerce.Inventory.Application;
public sealed record ManufacturingReceipt(Guid OperationId, Guid ProductionId, Guid WarehouseId, Guid VariantId, decimal Quantity, DateTimeOffset? ExpiresAt);
