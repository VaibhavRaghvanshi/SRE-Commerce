namespace SRECommerce.Catalog.Application;
public interface ICatalogService
{
    Task<object> Lookups(Guid tenant,bool publicRead,CancellationToken ct);
    Task<CatalogLookup> Category(Guid tenant,Guid? id,CategoryRequest request,CancellationToken ct);
    Task<CatalogLookup> Brand(Guid tenant,Guid? id,BrandRequest request,CancellationToken ct);
    Task<object> Attribute(Guid tenant,Guid? id,AttributeRequest request,CancellationToken ct);
    Task Bind(Guid tenant,Guid category,string version,AttributeBinding[] bindings,CancellationToken ct);
    Task<object> AttributesFor(Guid tenant,Guid category,CancellationToken ct);
    Task<CatalogPage<ProductView>> Products(Guid tenant,CatalogQuery query,bool publicRead,CancellationToken ct);
    Task<ProductView> Product(Guid tenant,Guid id,CancellationToken ct);
    Task<ProductChoice[]> ProductChoices(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<ProductView> Save(Guid tenant,Guid? id,ProductDraft request,CancellationToken ct);
    Task<ProductView> Publish(Guid tenant,Guid id,string version,CancellationToken ct);
    Task<Guid[]> PublishBatch(Guid tenant,PublishBatchRequest request,CancellationToken ct);
    Task<SRECommerce.BuildingBlocks.Contracts.PublishedSku[]> PublishedSkus(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<ProductView[]> ShoppingProducts(Guid tenant,Guid[] ids,bool publicRead,CancellationToken ct);
    Task<CounterItem> CounterLookup(Guid tenant, string code, CancellationToken ct);
    Task<ProductView[]> PublicProductsById(Guid tenant,Guid[] productIds,CancellationToken ct);
    Task Archive(Guid tenant,Guid id,string version,CancellationToken ct);
    Task<ProductView> PublicProduct(Guid tenant,string slug,CancellationToken ct);
    Task<CatalogPage<ProductView>> BuyerProducts(Guid tenant,CatalogQuery query,CancellationToken ct);
    Task<ProductView> BuyerProduct(Guid tenant,string slug,CancellationToken ct);
    Task<ProductView[]> BuyerProductsById(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<ProductView> SellableVariant(Guid tenant,Guid id,CancellationToken ct);
    Task<ProductView[]> SellableProducts(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<ProductView> PublishedVariantForStaff(Guid tenant,Guid id,CancellationToken ct);
    Task<Guid[]> PublicVariantIds(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<Guid[]> ShoppingVariantIds(Guid tenant,Guid[] ids,CancellationToken ct);
    Task<object> Collection(Guid tenant,Guid? id,CollectionRequest request,CancellationToken ct);
    Task<object> Import(Guid tenant,ImportRequest request,CancellationToken ct);
}
