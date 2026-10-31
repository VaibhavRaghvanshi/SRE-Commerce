namespace SRECommerce.Catalog.Application;
public record ImportRequest(Guid Key,string Source,ProductDraft[] Products);
