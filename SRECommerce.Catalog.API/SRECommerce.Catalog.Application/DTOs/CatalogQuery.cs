namespace SRECommerce.Catalog.Application;
public record CatalogQuery(int Page=1,int PageSize=24,string? Search=null,Guid? CategoryId=null,Guid? BrandId=null,string? Collection=null);
