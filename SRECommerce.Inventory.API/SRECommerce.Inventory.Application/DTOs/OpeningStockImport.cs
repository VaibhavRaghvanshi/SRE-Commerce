namespace SRECommerce.Inventory.Application;
public sealed record OpeningStockImport(Guid Key,string Source,Guid WarehouseId,string Lot,OpeningStockRow[] Rows);
