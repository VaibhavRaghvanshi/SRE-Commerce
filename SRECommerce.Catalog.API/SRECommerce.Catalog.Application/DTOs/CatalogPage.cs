namespace SRECommerce.Catalog.Application;
public record CatalogPage<T>(IReadOnlyCollection<T> Items,int Total,int Page,int PageSize);
