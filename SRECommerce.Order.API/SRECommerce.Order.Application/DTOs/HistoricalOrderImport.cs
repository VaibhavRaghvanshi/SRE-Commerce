using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record HistoricalOrderImport(Guid Key,string Source,HistoricalOrderRow[] Orders);
