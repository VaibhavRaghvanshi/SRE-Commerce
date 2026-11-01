namespace SRECommerce.Order.Application;
public sealed record HistoricalSourceInfo(string Sha256, string OrderId, string CustomerId, string Status,
    string CreatedAt, string ShippingAddress, bool TaxSplitRecorded, HistoricalSourceLine[] Items);
