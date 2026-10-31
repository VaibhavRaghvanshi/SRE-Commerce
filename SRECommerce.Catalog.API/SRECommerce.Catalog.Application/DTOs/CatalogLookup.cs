namespace SRECommerce.Catalog.Application;
public record CatalogLookup(Guid Id,string Name,string Slug,bool Archived,string Version,Guid? ParentId=null);
