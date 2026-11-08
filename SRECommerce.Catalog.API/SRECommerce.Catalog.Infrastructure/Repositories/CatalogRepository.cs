using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Domain;
namespace SRECommerce.Catalog.Infrastructure;
public sealed class CatalogRepository(CatalogDbContext db,ModuleScope scope,IHttpContextAccessor contexts,ServiceSpec service):ICatalogRepository
{
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var result=await action(ct);await tx.CommitAsync(ct);return result;}
    public async Task<IReadOnlyCollection<Category>> Categories(CancellationToken ct)=>await db.Categories.OrderBy(c=>c.Name).ToListAsync(ct);
    public async Task<IReadOnlyCollection<Brand>> Brands(CancellationToken ct)=>await db.Brands.OrderBy(b=>b.Name).ToListAsync(ct);
    public async Task<IReadOnlyCollection<AttributeDefinition>> Definitions(CancellationToken ct)=>await db.Attributes.OrderBy(a=>a.Code).ToListAsync(ct);
    public async Task<IReadOnlyCollection<CategoryAttribute>> Bindings(Guid category,CancellationToken ct)=>await db.Bindings.Where(b=>b.CategoryId==category).ToListAsync(ct);
    public Task<bool> AttributeBound(Guid id,CancellationToken ct)=>db.Bindings.AnyAsync(b=>b.AttributeId==id&&b.Active,ct);
    private IQueryable<Product> Aggregate()=>db.Products.Include(p=>p.Variants).Include(p=>p.Media).Include(p=>p.BundleItems).Include(p=>p.Relationships).AsSplitQuery();
    public Task<Product?> Product(Guid id,CancellationToken ct)=>Aggregate().SingleOrDefaultAsync(p=>p.Id==id,ct);
    public Task<ProductChoice[]> ProductChoices(Guid[] ids,CancellationToken ct)=>db.Products.AsNoTracking().Where(p=>ids.Contains(p.Id))
        .OrderBy(p=>p.Name).ThenBy(p=>p.Id).Select(p=>new ProductChoice(p.Id,p.Name,p.Slug,p.Status,p.PublishedRevision)).ToArrayAsync(ct);
    public Task<ProductVariant?> Variant(Guid id,CancellationToken ct)=>db.Variants.SingleOrDefaultAsync(v=>v.Id==id,ct);
    public async Task<CatalogPage<Product>> Drafts(CatalogQuery query,CancellationToken ct)
    {
        var all=Aggregate();if(query.CategoryId is {} category)all=all.Where(p=>p.CategoryId==category);if(query.BrandId is {} brand)all=all.Where(p=>p.BrandId==brand);
        if(!string.IsNullOrWhiteSpace(query.Search))all=all.Where(p=>p.Name.Contains(query.Search)||p.Slug.Contains(query.Search));
        var total=await all.CountAsync(ct);return new(await all.OrderBy(p=>p.Name).ThenBy(p=>p.Id).Skip((query.Page-1)*query.PageSize).Take(query.PageSize).ToListAsync(ct),total,query.Page,query.PageSize);
    }
    private IQueryable<ProductPublication> Active(bool b2b=false,IQueryable<ProductPublication>? publications=null)=>from snapshot in (publications??db.Publications).AsNoTracking()
        join product in db.Products.AsNoTracking() on new{snapshot.TenantId,Id=snapshot.ProductId,Revision=snapshot.Revision} equals new{product.TenantId,product.Id,Revision=product.PublishedRevision}
        where product.Status!=ProductStatus.Archived&&snapshot.Visible&&(b2b||!snapshot.B2BOnly)&&db.Categories.Any(c=>c.Id==snapshot.CategoryId&&!c.Archived)&&(snapshot.BrandId==null||db.Brands.Any(b=>b.Id==snapshot.BrandId&&!b.Archived))
        select snapshot;
    public async Task<CounterItem[]> CounterItems(Guid tenant, string code, CancellationToken ct)
    {
        // Scan the immutable publication, since a draft can change its barcode before it goes live.
        var matching = db.Publications.FromSql($"""
            SELECT p.* FROM dbo.ProductPublications AS p
            WHERE p.TenantId = {tenant} AND EXISTS (
                SELECT 1 FROM OPENJSON(p.Document, '$.variants')
                WITH (Sku nvarchar(64) '$.sku', Barcode nvarchar(64) '$.barcode', Active bit '$.active') AS v
                WHERE v.Active = 1 AND (v.Sku COLLATE Latin1_General_100_BIN2 = {code} OR v.Barcode COLLATE Latin1_General_100_BIN2 = {code})
            )
            """);
        var rows = await Active(publications: matching).OrderBy(p => p.ProductId).Select(p => p.Document).Take(2).ToArrayAsync(ct);
        return rows.Select(ContractJson.Read<ProductView>).SelectMany(product => product.Variants
            .Where(v => v.Active && v.Id is not null && (v.Sku == code || v.Barcode == code))
            .Select(v => new CounterItem(v.Id!.Value, product.Name, v.Sku))).Take(2).ToArray();
    }
    public async Task<CatalogPage<ProductView>> Published(CatalogQuery query,CancellationToken ct,bool includeB2B=false)
    {
        var all=Active(includeB2B);if(query.CategoryId is {} category)all=all.Where(p=>p.CategoryId==category);if(query.BrandId is {} brand)all=all.Where(p=>p.BrandId==brand);
        if(!string.IsNullOrWhiteSpace(query.Search))all=all.Where(p=>p.Name.Contains(query.Search)||p.Slug.Contains(query.Search));
        if(!string.IsNullOrWhiteSpace(query.Collection))all=all.Where(p=>db.Collections.Any(c=>c.Slug==query.Collection&&!c.Archived&&c.Products.Any(cp=>cp.ProductId==p.ProductId&&cp.Active)));
        var ordered=all.OrderBy(p=>p.Name).ThenBy(p=>p.ProductId);
        if(!string.IsNullOrWhiteSpace(query.Collection))ordered=all.OrderBy(p=>db.Set<CollectionProduct>()
            .Where(member=>member.ProductId==p.ProductId&&member.Active&&db.Collections.Any(c=>c.Id==member.CollectionId&&c.Slug==query.Collection&&!c.Archived))
            .Select(member=>member.Order).FirstOrDefault()).ThenBy(p=>p.Name).ThenBy(p=>p.ProductId);
        var total=await all.CountAsync(ct);var rows=await ordered.Skip((query.Page-1)*query.PageSize).Take(query.PageSize).Select(p=>p.Document).ToListAsync(ct);
        return new(rows.Select(ContractJson.Read<ProductView>).ToArray(),total,query.Page,query.PageSize);
    }
    public async Task<ProductView?> PublishedProduct(string slug,CancellationToken ct,bool includeB2B=false)
    {var json=await Active(includeB2B).Where(p=>p.Slug==slug).Select(p=>p.Document).SingleOrDefaultAsync(ct);return json is null?null:ContractJson.Read<ProductView>(json);}
    public async Task<ProductView?> PublishedVariant(Guid id,CancellationToken ct)
    {
        var json=await(from variant in db.Variants.AsNoTracking() join snapshot in Active(true) on new{variant.TenantId,Id=variant.ProductId} equals new{snapshot.TenantId,Id=snapshot.ProductId} where variant.Id==id select snapshot.Document).SingleOrDefaultAsync(ct);
        if(json is null)return null;var value=ContractJson.Read<ProductView>(json);return value.Variants.Any(v=>v.Id==id&&v.Active)?value:null;
    }
    public async Task<Guid[]> VisibleVariantIds(Guid[] ids,bool includeB2B,CancellationToken ct)
    {
        var rows=await(from variant in db.Variants.AsNoTracking() join snapshot in Active(includeB2B) on new{variant.TenantId,Id=variant.ProductId} equals new{snapshot.TenantId,Id=snapshot.ProductId} where ids.Contains(variant.Id) select new{variant.Id,snapshot.Document}).ToListAsync(ct);
        return rows.Where(row=>ContractJson.Read<ProductView>(row.Document).Variants.Any(v=>v.Id==row.Id&&v.Active)).Select(row=>row.Id).Distinct().ToArray();
    }
    public async Task<PublishedSku[]> PublishedSkus(Guid[] ids,CancellationToken ct)
    {
        var rows=await(from variant in db.Variants.AsNoTracking() join snapshot in Active(true) on new{variant.TenantId,Id=variant.ProductId} equals new{snapshot.TenantId,Id=snapshot.ProductId} where ids.Contains(variant.Id) select new{variant.Id,snapshot.Document}).ToListAsync(ct);
        return rows.Select(row=>(row.Id,Product:ContractJson.Read<ProductView>(row.Document))).SelectMany(row=>row.Product.Variants.Where(v=>v.Id==row.Id&&v.Active).Select(v=>new PublishedSku(row.Product.Id,row.Id,v.Sku,v.Barcode,(int)row.Product.Kind))).ToArray();
    }
    public async Task<ProductView[]> ShoppingProducts(Guid[] ids,bool includeB2B,CancellationToken ct)
    {
        var documents=await(from variant in db.Variants.AsNoTracking() join snapshot in Active(includeB2B) on new{variant.TenantId,Id=variant.ProductId} equals new{snapshot.TenantId,Id=snapshot.ProductId} where ids.Contains(variant.Id) select snapshot.Document).Distinct().ToListAsync(ct);
        return documents.Select(ContractJson.Read<ProductView>).Where(p=>p.Variants.Any(v=>v.Active&&v.Id is {} id&&ids.Contains(id))).ToArray();
    }
    public async Task<ProductView[]> PublicProductsById(Guid[] productIds,CancellationToken ct,bool includeB2B=false)
    {
        var rows=await Active(includeB2B).Where(p=>productIds.Contains(p.ProductId)).Select(p=>p.Document).ToArrayAsync(ct);
        return rows.Select(ContractJson.Read<ProductView>).ToArray();
    }
    public async Task<IReadOnlyCollection<ProductCollection>> Collections(CancellationToken ct)=>await db.Collections.Include(c=>c.Products).OrderBy(c=>c.Name).ToListAsync(ct);
    public Task<CatalogImport?> Import(Guid id,CancellationToken ct)=>db.Imports.SingleOrDefaultAsync(i=>i.Id==id,ct);
    public void Add<T>(T entity)where T:class=>db.Add(entity);
    public void Version(BuildingBlocks.Kernel.IVersionedRecord entity,string? expected)
    {
        byte[] value;try{value=Convert.FromBase64String(expected??"");}catch(FormatException){throw new BusinessFault("InvalidVersion",400);}
        if(value.Length!=8)throw new BusinessFault("VersionRequired",400);if(!CryptographicOperations.FixedTimeEquals(value,entity.Version))throw new BusinessFault("VersionConflict");
        db.Entry(entity).Property("Version").OriginalValue=value;
    }
    public async Task Commit(string action,Guid resource,object? publishedEvent,CancellationToken ct)
    {db.Record(scope,contexts,action,resource.ToString());if(publishedEvent is not null)db.Emit(scope,service,contexts,action+".v1",publishedEvent);await db.SaveChangesAsync(ct);}
}
