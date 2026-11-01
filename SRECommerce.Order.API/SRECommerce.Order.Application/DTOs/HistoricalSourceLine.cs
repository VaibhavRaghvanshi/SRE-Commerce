namespace SRECommerce.Order.Application;
public sealed record HistoricalSourceLine(string ItemId, string ProductId, string? ImageUrl, decimal DiscountPerUnit);
