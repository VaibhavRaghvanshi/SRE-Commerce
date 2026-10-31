namespace SRECommerce.Inventory.Application;
public record PurchaseReceiveRequest(Guid OperationId,string Version,PurchaseReceiptInput[] Lines);
public record PurchaseReceiptInput(Guid LineId,decimal Quantity,string Lot,DateTimeOffset? ExpiresAt);
