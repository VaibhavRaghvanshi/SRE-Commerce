namespace SRECommerce.Order.Application;

public interface ISalesReportRepository { Task<SalesReport> Read(SalesReportQuery query, CancellationToken ct); }
