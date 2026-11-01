namespace SRECommerce.Order.Application;
public sealed record ReturnLine(Guid VariantId,decimal Quantity,decimal RefundAmount);
