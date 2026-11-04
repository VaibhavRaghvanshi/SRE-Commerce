using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.Order.Application;

public sealed class SalesReportService(ISalesReportRepository reports, IOrderAccess access, TimeProvider clock)
{
    public async Task<SalesReport> Read(Guid tenant, SalesReportQuery query, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ActorPermission.Manager, ct);
        if (query.From == default || query.Through < query.From || query.Through.DayNumber - query.From.DayNumber > 365 ||
            query.Through > DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime) || query.Currency is null ||
            query.Currency.Length != 3 || query.Currency.Any(c => c is < 'A' or > 'Z'))
            throw new BusinessFault("InvalidSalesReportRange", 400);
        return await reports.Read(query, ct);
    }
}
