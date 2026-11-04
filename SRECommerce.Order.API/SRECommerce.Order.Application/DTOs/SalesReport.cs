using SRECommerce.Order.Domain;

namespace SRECommerce.Order.Application;

public sealed record SalesReportQuery(DateOnly From, DateOnly Through, string Currency = "INR", bool IncludeHistorical = false);
public sealed record SalesTotals(int PlacedOrders, int AcceptedOrders, int GuestOrders, int HistoricalOrders,
    decimal AcceptedOrderTotal, decimal RefundsForPlacedOrders, decimal CapturedStateBalance);
public sealed record SalesDay(DateTime Date, int PlacedOrders, int AcceptedOrders, decimal AcceptedOrderTotal, decimal RefundsForPlacedOrders);
public sealed record SalesStatus(OrderStatus Status, int Orders, decimal OrderTotal);
public sealed record ProductSales(Guid ProductId, Guid VariantId, string Name, string Sku, decimal Quantity, decimal LineTotal);
public sealed record SalesReport(DateOnly From, DateOnly Through, string Currency, bool IncludesHistorical,
    string TimeZone, DateTimeOffset GeneratedAt, SalesTotals Totals, SalesDay[] Days, SalesStatus[] States, ProductSales[] Products);
