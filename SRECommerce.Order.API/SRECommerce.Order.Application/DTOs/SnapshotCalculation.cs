using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record SnapshotCalculation(Guid? PromotionId,string? PromotionCode,decimal Subtotal,decimal Discount,decimal Net,decimal Tax,decimal Total,SnapshotCalculatedLine[] Lines);
