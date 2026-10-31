namespace SRECommerce.Pricing.Application;
public record PricePage<T>(IReadOnlyCollection<T> Items,int Page,int PageSize,int Total);
