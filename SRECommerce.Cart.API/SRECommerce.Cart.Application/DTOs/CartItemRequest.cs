namespace SRECommerce.Cart.Application;
public sealed record CartItemRequest(Guid OperationId,Guid VariantId,decimal Quantity,string Version);
