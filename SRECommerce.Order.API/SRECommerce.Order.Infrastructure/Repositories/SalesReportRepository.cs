using Microsoft.EntityFrameworkCore;
using SRECommerce.Order.Application;
using SRECommerce.Order.Domain;

namespace SRECommerce.Order.Infrastructure;

public sealed class SalesReportRepository(OrderDbContext db, TimeProvider clock) : ISalesReportRepository
{
    private static readonly OrderStatus[] Accepted = [OrderStatus.Confirmed, OrderStatus.Processing, OrderStatus.PartiallyFulfilled,
        OrderStatus.Fulfilled, OrderStatus.Delivered, OrderStatus.Completed];
    private static readonly PaymentState[] Captured = [PaymentState.Captured, PaymentState.PartiallyRefunded, PaymentState.Refunded];
    public async Task<SalesReport> Read(SalesReportQuery query, CancellationToken ct)
    {
        var start = new DateTimeOffset(query.From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var end = new DateTimeOffset(query.Through.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var orders = db.Orders.AsNoTracking().Where(row => row.CreatedAt >= start && row.CreatedAt < end &&
            row.Currency == query.Currency && (query.IncludeHistorical || !row.Historical));
        var totals = await orders.GroupBy(_ => 1).Select(group => new SalesTotals(
            group.Count(), group.Count(row => Accepted.Contains(row.Status)), group.Count(row => row.UserId == null), group.Count(row => row.Historical),
            group.Sum(row => Accepted.Contains(row.Status) ? row.Total : 0), group.Sum(row => row.Refunded),
            group.Sum(row => Captured.Contains(row.PaymentState) ? row.Total - row.Refunded : 0))).SingleOrDefaultAsync(ct)
            ?? new(0, 0, 0, 0, 0, 0, 0);
        var days = await orders.GroupBy(row => EF.Functions.AtTimeZone(row.CreatedAt, "UTC").Date).OrderBy(group => group.Key).Select(group => new SalesDay(group.Key,
            group.Count(), group.Count(row => Accepted.Contains(row.Status)), group.Sum(row => Accepted.Contains(row.Status) ? row.Total : 0),
            group.Sum(row => row.Refunded))).ToArrayAsync(ct);
        days = days.Select(day => day with { Date = DateTime.SpecifyKind(day.Date, DateTimeKind.Utc) }).ToArray();
        var states = await orders.GroupBy(row => row.Status).OrderBy(group => group.Key)
            .Select(group => new SalesStatus(group.Key, group.Count(), group.Sum(row => row.Total))).ToArrayAsync(ct);
        var productRows = await (from order in orders where Accepted.Contains(order.Status)
            from item in order.Items
            group item by new { item.ProductId, item.VariantId, item.Name, item.Sku } into sales
            select new { sales.Key.ProductId, sales.Key.VariantId, sales.Key.Name, sales.Key.Sku,
                Quantity = sales.Sum(item => item.Quantity), LineTotal = sales.Sum(item => item.Total) })
            .OrderByDescending(row => row.LineTotal).ThenBy(row => row.VariantId).Take(20).ToArrayAsync(ct);
        var products = productRows.Select(row => new ProductSales(row.ProductId, row.VariantId, row.Name, row.Sku, row.Quantity, row.LineTotal)).ToArray();
        return new(query.From, query.Through, query.Currency, query.IncludeHistorical, "UTC", clock.GetUtcNow(), totals, days, states, products);
    }
}
