namespace SRECommerce.Inventory.Application;
public record ReserveRequest(Guid Id,Guid WarehouseId,string Reference,DateTimeOffset ExpiresAt,ReservationInput[] Items);
public record ReservationInput(Guid VariantId,decimal Quantity);
