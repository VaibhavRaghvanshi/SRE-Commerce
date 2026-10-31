namespace SRECommerce.Catalog.Application;
public record CollectionRequest(string Name,string Slug,Guid[] ProductIds,string? Version=null,bool Archived=false);
