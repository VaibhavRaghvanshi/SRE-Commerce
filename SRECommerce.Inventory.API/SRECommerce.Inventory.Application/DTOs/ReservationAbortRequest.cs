namespace SRECommerce.Inventory.Application;
public sealed record ReservationAbortRequest(Guid OperationId,string Reason);
