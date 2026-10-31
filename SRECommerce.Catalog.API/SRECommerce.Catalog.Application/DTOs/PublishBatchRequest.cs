namespace SRECommerce.Catalog.Application;
public sealed record PublishBatchRequest(Guid Key,string Source,PublishBatchItem[] Products);
