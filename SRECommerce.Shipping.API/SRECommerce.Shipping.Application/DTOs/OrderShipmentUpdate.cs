namespace SRECommerce.Shipping.Application;
public sealed record OrderShipmentUpdate(Guid OperationId,string Version,int Status,string? CourierReference);
