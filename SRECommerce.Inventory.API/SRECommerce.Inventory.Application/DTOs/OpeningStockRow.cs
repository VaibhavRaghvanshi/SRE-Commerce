namespace SRECommerce.Inventory.Application;
public sealed record OpeningStockRow(Guid ProductId,Guid VariantId,decimal Quantity);
