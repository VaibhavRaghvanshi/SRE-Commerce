namespace SRECommerce.BuildingBlocks.Contracts;

public sealed record StockAlertEvent(Guid VariantId, string Sku, decimal Available, decimal ReorderLevel, long Episode, DateTimeOffset ObservedAt);
