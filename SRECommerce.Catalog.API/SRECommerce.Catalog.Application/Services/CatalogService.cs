using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Catalog.Domain;
namespace SRECommerce.Catalog.Application;
public sealed class CatalogService(ICatalogRepository repository,ICatalogAccess access):ICatalogService
{
    private static string Json<T>(T value)=>ContractJson.Write(value);
    private static T Read<T>(string value)=>ContractJson.Read<T>(value);
    private static CatalogLookup Lookup(Domain.Category value)=>new(value.Id,value.Name,value.Slug,value.Archived,Convert.ToBase64String(value.Version),value.ParentId);
    private static CatalogLookup Lookup(Domain.Brand value)=>new(value.Id,value.Name,value.Slug,value.Archived,Convert.ToBase64String(value.Version));
    public static ProductView View(Domain.Product value,bool publicRead=false)=>new(value.Id,value.Name,value.Slug,value.CategoryId,value.BrandId,value.Kind,value.Status,value.Description,value.SeoTitle,value.SeoDescription,value.Visible,value.B2BOnly,Read<Dictionary<string,JsonElement>>(value.Attributes),Read<Dictionary<string,string>>(value.Specifications),
        value.Variants.Where(v=>v.Active).OrderBy(v=>v.Sku).Select(v=>new VariantInput(v.Id,v.Sku,Read<Dictionary<string,string>>(v.Options),v.Barcode)).ToArray(),
        value.Media.Where(m=>m.Active).OrderBy(m=>m.Order).Select(m=>new MediaInput(m.Id,m.Url,m.Alt,m.Order)).ToArray(),
        value.BundleItems.Where(b=>b.Active).OrderBy(b=>b.ComponentVariantId).Select(b=>new BundleInput(b.ComponentVariantId,b.Quantity)).ToArray(),
        value.Relationships.Where(r=>r.Active).OrderBy(r=>r.RelatedProductId).Select(r=>new RelationshipInput(r.RelatedProductId,r.Kind)).ToArray(),value.PublishedRevision,publicRead?null:Convert.ToBase64String(value.Version));
    private static void Query(CatalogQuery query)
    {if(query.Page is <1 or >10000||query.PageSize is <1 or >100||query.Search?.Length>100)throw new BuildingBlocks.Kernel.DomainFault("InvalidCatalogQuery");}
    public async Task<object> Lookups(Guid tenant,bool publicRead,CancellationToken ct)
    {
        if(publicRead)await access.Public(tenant,ct);else await access.Authorize(tenant,ActorPermission.Staff,ct);
        var categories=(await repository.Categories(ct)).Where(c=>!publicRead||!c.Archived).Select(Lookup).ToArray();
        var brands=(await repository.Brands(ct)).Where(b=>!publicRead||!b.Archived).Select(Lookup).ToArray();
        var definitions=publicRead?null:(object)(await repository.Definitions(ct)).Select(d=>new{d.Id,d.Code,d.Name,d.DataType,Options=Read<string[]>(d.Options),d.Unit,d.Archived,Version=Convert.ToBase64String(d.Version)}).ToArray();
        var collections=(await repository.Collections(ct)).Where(c=>!publicRead||!c.Archived).Select(c=>new{c.Id,c.Name,c.Slug,c.Archived,ProductIds=c.Products.Where(p=>p.Active).OrderBy(p=>p.Order).Select(p=>p.ProductId).ToArray(),Version=publicRead?null:Convert.ToBase64String(c.Version)}).ToArray();
        return new{categories,brands,definitions,collections};
    }
    public async Task<CatalogLookup> Category(Guid tenant,Guid? id,CategoryRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);
        return await repository.Transaction(async token=>
        {
            var all=await repository.Categories(token);var value=id is null?new Domain.Category{TenantId=tenant}:all.SingleOrDefault(c=>c.Id==id)??throw new BusinessFault("CategoryNotFound",404);
            if(id is not null)repository.Version(value,request.Version);else repository.Add(value);
            if(request.ParentId is {} parent)
            {
                var visited=new HashSet<Guid>{value.Id};Guid? current=parent;
                while(current is {} node){if(!visited.Add(node))throw new BusinessFault("CategoryCycle");var ancestor=all.SingleOrDefault(c=>c.Id==node&&!c.Archived)??throw new BusinessFault("ParentCategoryUnavailable");current=ancestor.ParentId;}
            }
            if(request.Archived&&all.Any(c=>c.ParentId==value.Id&&!c.Archived))throw new BusinessFault("ArchiveChildCategoriesFirst");
            value.Name=CatalogRules.Text(request.Name);value.Slug=CatalogRules.Slug(request.Slug);value.ParentId=request.ParentId;value.Archived=request.Archived;
            await repository.Commit("catalog.category.saved",value.Id,null,token);return Lookup(value);
        },ct);
    }
    public async Task<CatalogLookup> Brand(Guid tenant,Guid? id,BrandRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);return await repository.Transaction(async token=>
        {
            var value=id is null?new Domain.Brand{TenantId=tenant}:(await repository.Brands(token)).SingleOrDefault(b=>b.Id==id)??throw new BusinessFault("BrandNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            value.Name=CatalogRules.Text(request.Name);value.Slug=CatalogRules.Slug(request.Slug);value.Archived=request.Archived;
            await repository.Commit("catalog.brand.saved",value.Id,null,token);return Lookup(value);
        },ct);
    }
    public async Task<object> Attribute(Guid tenant,Guid? id,AttributeRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);return await repository.Transaction(async token=>
        {
            if(!Enum.IsDefined(request.DataType)||request.Options is null||request.Options.Length>100||request.Options.Distinct(StringComparer.Ordinal).Count()!=request.Options.Length)throw new BuildingBlocks.Kernel.DomainFault("InvalidAttributeDefinition");
            if(request.DataType==AttributeDataType.Choice&&request.Options.Length==0)throw new BuildingBlocks.Kernel.DomainFault("ChoiceOptionsRequired");
            foreach(var option in request.Options)CatalogRules.Text(option,80);
            var value=id is null?new AttributeDefinition{TenantId=tenant}:(await repository.Definitions(token)).SingleOrDefault(a=>a.Id==id)??throw new BusinessFault("AttributeNotFound",404);
            if(id is null)repository.Add(value);else{repository.Version(value,request.Version);if(value.Code!=CatalogRules.Slug(request.Code)||value.DataType!=request.DataType)throw new BusinessFault("AttributeIdentityAndTypeImmutable");}
            value.Code=CatalogRules.Slug(request.Code);value.Name=CatalogRules.Text(request.Name);value.DataType=request.DataType;value.Options=Json(request.Options);value.Unit=request.Unit is null?null:CatalogRules.Text(request.Unit,30);value.Archived=request.Archived;
            await repository.Commit("catalog.attribute.saved",value.Id,null,token);return new{value.Id,value.Code,value.Name,value.DataType,Options=request.Options,value.Unit,value.Archived,Version=Convert.ToBase64String(value.Version)};
        },ct);
    }
    public async Task Bind(Guid tenant,Guid category,string version,AttributeBinding[] bindings,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);await repository.Transaction(async token=>
        {
            if(bindings is null||bindings.Length>60||bindings.Select(b=>b.AttributeId).Distinct().Count()!=bindings.Length)throw new BuildingBlocks.Kernel.DomainFault("InvalidAttributeBindings");
            var value=(await repository.Categories(token)).SingleOrDefault(c=>c.Id==category&&!c.Archived)??throw new BusinessFault("CategoryNotFound",404);repository.Version(value,version);
            var definitions=await repository.Definitions(token);var existing=await repository.Bindings(category,token);
            foreach(var row in existing)row.Active=false;
            foreach(var binding in bindings)
            {
                if(!definitions.Any(d=>d.Id==binding.AttributeId&&!d.Archived))throw new BusinessFault("AttributeUnavailable");
                var row=existing.SingleOrDefault(b=>b.AttributeId==binding.AttributeId);if(row is null){row=new(){TenantId=tenant,CategoryId=category,AttributeId=binding.AttributeId};repository.Add(row);}row.Active=true;row.Required=binding.Required;
            }
            // Touch the principal even when only mapping rows changed, so its rowversion is a useful write precondition.
            value.BindingRevision++;await repository.Commit("catalog.category.attributes",value.Id,null,token);return true;
        },ct);
    }
    public async Task<CatalogPage<ProductView>> Products(Guid tenant,CatalogQuery query,bool publicRead,CancellationToken ct)
    {
        Query(query);if(publicRead){await access.Public(tenant,ct);return await repository.Published(query,ct);}
        await access.Authorize(tenant,ActorPermission.Staff,ct);var result=await repository.Drafts(query,ct);return new(result.Items.Select(p=>View(p)).ToArray(),result.Total,result.Page,result.PageSize);
    }
    public async Task<ProductView> Product(Guid tenant,Guid id,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Staff,ct);return View(await repository.Product(id,ct)??throw new BusinessFault("ProductNotFound",404));}
    private async Task<IReadOnlyCollection<(AttributeDefinition Definition,bool Required)>> AttributeBindings(Guid category,CancellationToken ct)
    {
        var categories=await repository.Categories(ct);var definitions=await repository.Definitions(ct);var result=new Dictionary<Guid,(AttributeDefinition,bool)>();Guid? current=category;var visited=new HashSet<Guid>();
        while(current is {} id)
        {
            if(!visited.Add(id))throw new BusinessFault("CategoryCycle");var row=categories.SingleOrDefault(c=>c.Id==id&&!c.Archived)??throw new BusinessFault("CategoryUnavailable");
            foreach(var binding in (await repository.Bindings(id,ct)).Where(b=>b.Active))
            {var definition=definitions.Single(d=>d.Id==binding.AttributeId);if(!result.TryGetValue(definition.Id,out var prior))result.Add(definition.Id,(definition,binding.Required));else result[definition.Id]=(definition,prior.Item2||binding.Required);}
            current=row.ParentId;
        }
        return result.Values.ToArray();
    }
    public async Task<object> AttributesFor(Guid tenant,Guid category,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Staff,ct);return(await AttributeBindings(category,ct)).Select(b=>new{b.Definition.Id,b.Definition.Code,b.Definition.Name,b.Definition.DataType,Options=Read<string[]>(b.Definition.Options),b.Definition.Unit,b.Required}).ToArray();}
    private async Task<Domain.Product> SaveCore(Guid tenant,Guid? id,ProductDraft request,CancellationToken ct)
    {
        if(!Enum.IsDefined(request.Kind)||request.Variants is null||request.Media is null||request.BundleItems is null||request.Relationships is null||request.Variants.Length is <1 or >100||request.Media.Length>20||request.BundleItems.Length>40||request.Relationships.Length>40)throw new BuildingBlocks.Kernel.DomainFault("InvalidProductShape");
        if(request.Kind!=ProductKind.Variable&&request.Variants.Length!=1)throw new BusinessFault("SingleSellableVariantRequired");
        if(request.Kind!=ProductKind.Bundle&&request.BundleItems.Length>0||request.Kind==ProductKind.Bundle&&request.BundleItems.Length==0)throw new BusinessFault("InvalidBundleComponents");
        var product=id is null?new Domain.Product{TenantId=tenant}:await repository.Product(id.Value,ct)??throw new BusinessFault("ProductNotFound",404);
        if(product.Status==ProductStatus.Archived)throw new BusinessFault("ProductArchived");if(id is null)repository.Add(product);else repository.Version(product,request.Version);
        if(product.PublishedRevision>0&&product.Kind!=request.Kind)throw new BusinessFault("PublishedProductKindImmutable");
        if(product.PublishedRevision>0&&product.Slug!=CatalogRules.Slug(request.Slug))throw new BusinessFault("PublishedSlugImmutable");
        var bindings=await AttributeBindings(request.CategoryId,ct);CatalogRules.Attributes(request.Attributes,bindings,false);CatalogRules.Specification(request.Specifications);
        if(request.BrandId is {} brand&&!(await repository.Brands(ct)).Any(b=>b.Id==brand&&!b.Archived))throw new BusinessFault("BrandUnavailable");
        product.Name=CatalogRules.Text(request.Name);product.Slug=CatalogRules.Slug(request.Slug);product.CategoryId=request.CategoryId;product.BrandId=request.BrandId;product.Kind=request.Kind;product.Description=CatalogRules.Text(request.Description,10000,true);
        product.SeoTitle=CatalogRules.Text(request.SeoTitle,160,true);product.SeoDescription=CatalogRules.Text(request.SeoDescription,320,true);product.Visible=request.Visible;product.B2BOnly=request.B2BOnly;product.Attributes=Json(request.Attributes);product.Specifications=Json(request.Specifications);product.UpdatedAt=DateTimeOffset.UtcNow;
        if(product.Attributes.Length>40000||product.Specifications.Length>65536)throw new BusinessFault("ProductFieldsTooLarge",400);
        var seenSkus=new HashSet<string>(StringComparer.Ordinal);var seenVariants=new HashSet<Guid>();var seenOptions=new HashSet<string>(StringComparer.Ordinal);
        foreach(var input in request.Variants)
        {
            var sku=CatalogRules.Sku(input.Sku);var options=Json(CatalogRules.VariantOptions(input.Options));if(!seenSkus.Add(sku)||!seenOptions.Add(options))throw new BusinessFault("DuplicateVariant");
            var variant=input.Id is null?new ProductVariant{TenantId=tenant,ProductId=product.Id}:product.Variants.SingleOrDefault(v=>v.Id==input.Id)??throw new BusinessFault("VariantNotFound",404);
            if(!seenVariants.Add(variant.Id))throw new BusinessFault("DuplicateVariant");if(input.Id is null)product.Variants.Add(variant);else if(product.PublishedRevision>0&&variant.Sku!=sku)throw new BusinessFault("PublishedSkuImmutable");
            variant.Sku=sku;variant.Options=options;variant.Active=input.Active;variant.Barcode=input.Barcode is null?null:CatalogRules.Text(input.Barcode,64);
        }
        foreach(var variant in product.Variants.Where(v=>!seenVariants.Contains(v.Id)))variant.Active=false;
        if(!product.Variants.Any(v=>v.Active))throw new BusinessFault("ActiveVariantRequired");
        var mediaIds=new HashSet<Guid>();var orders=new HashSet<int>();foreach(var input in request.Media)
        {
            if(input.Order is <1 or >100||!orders.Add(input.Order))throw new BusinessFault("InvalidMediaOrder");var media=input.Id is null?new ProductMedia{TenantId=tenant,ProductId=product.Id}:product.Media.SingleOrDefault(m=>m.Id==input.Id)??throw new BusinessFault("MediaNotFound",404);
            if(!mediaIds.Add(media.Id))throw new BusinessFault("DuplicateMedia");if(input.Id is null)product.Media.Add(media);media.Url=CatalogRules.MediaUrl(input.Url);media.Alt=CatalogRules.Text(input.Alt,200);media.Order=input.Order;media.Active=true;
        }
        foreach(var media in product.Media.Where(m=>!mediaIds.Contains(m.Id)))media.Active=false;
        foreach(var row in product.BundleItems)row.Active=false;var components=new HashSet<Guid>();foreach(var input in request.BundleItems)
        {
            if(!components.Add(input.VariantId))throw new BusinessFault("DuplicateBundleComponent");BuildingBlocks.Kernel.DomainValues.Quantity(input.Quantity);
            var variant=await repository.Variant(input.VariantId,ct)??throw new BusinessFault("BundleVariantUnavailable");var owner=await repository.Product(variant.ProductId,ct);
            if(!variant.Active||owner is null||owner.Id==product.Id||owner.Kind==ProductKind.Bundle||owner.Status==ProductStatus.Archived)throw new BusinessFault("NestedOrUnavailableBundle");
            var row=product.BundleItems.SingleOrDefault(b=>b.ComponentVariantId==input.VariantId);if(row is null){row=new(){TenantId=tenant,ProductId=product.Id,ComponentVariantId=input.VariantId};product.BundleItems.Add(row);}row.Quantity=input.Quantity;row.Active=true;
        }
        foreach(var row in product.Relationships)row.Active=false;var relations=new HashSet<Guid>();foreach(var input in request.Relationships)
        {
            if(input.ProductId==product.Id||!relations.Add(input.ProductId)||input.Kind is not("related"or"upsell"or"cross-sell")||await repository.Product(input.ProductId,ct)is null)throw new BusinessFault("InvalidProductRelationship");
            var row=product.Relationships.SingleOrDefault(r=>r.RelatedProductId==input.ProductId);if(row is null){row=new(){TenantId=tenant,ProductId=product.Id,RelatedProductId=input.ProductId};product.Relationships.Add(row);}row.Kind=input.Kind;row.Active=true;
        }
        return product;
    }
    public async Task<ProductView> Save(Guid tenant,Guid? id,ProductDraft request,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Manager,ct);return await repository.Transaction(async token=>{var value=await SaveCore(tenant,id,request,token);await repository.Commit("catalog.product.draft",value.Id,null,token);return View(value);},ct);}
    public async Task<ProductView> Publish(Guid tenant,Guid id,string version,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);return await repository.Transaction(async token=>
        {
            var product=await repository.Product(id,token)??throw new BusinessFault("ProductNotFound",404);repository.Version(product,version);if(product.Status==ProductStatus.Archived)throw new BusinessFault("ProductArchived");
            CatalogRules.Attributes(Read<Dictionary<string,JsonElement>>(product.Attributes),await AttributeBindings(product.CategoryId,token),true);
            if(product.BrandId is {} brand&&!(await repository.Brands(token)).Any(b=>b.Id==brand&&!b.Archived))throw new BusinessFault("BrandUnavailable");
            foreach(var component in product.BundleItems.Where(b=>b.Active))
            {var item=await repository.PublishedVariant(component.ComponentVariantId,token)??throw new BusinessFault("BundleComponentNotPublished");if(!product.B2BOnly&&item.B2BOnly)throw new BusinessFault("BundleComponentRequiresB2B");}
            product.PublishedRevision++;product.Status=ProductStatus.Published;product.UpdatedAt=DateTimeOffset.UtcNow;var view=View(product,true);var json=Json(view);if(Encoding.UTF8.GetByteCount(json)>131072)throw new BusinessFault("ProductSnapshotTooLarge",400);
            repository.Add(new ProductPublication{TenantId=tenant,ProductId=id,Revision=product.PublishedRevision,Name=product.Name,Slug=product.Slug,CategoryId=product.CategoryId,BrandId=product.BrandId,Visible=product.Visible,B2BOnly=product.B2BOnly,Document=json,Hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))});
            await repository.Commit("catalog.product.published",id,view,token);return View(product);
        },ct);
    }
    public async Task Archive(Guid tenant,Guid id,string version,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Manager,ct);await repository.Transaction(async token=>{var value=await repository.Product(id,token)??throw new BusinessFault("ProductNotFound",404);repository.Version(value,version);value.Status=ProductStatus.Archived;value.UpdatedAt=DateTimeOffset.UtcNow;await repository.Commit("catalog.product.archived",id,new{productId=id,archived=true},token);return true;},ct);}
    public async Task<ProductView> PublicProduct(Guid tenant,string slug,CancellationToken ct)
    {await access.Public(tenant,ct);return await repository.PublishedProduct(CatalogRules.Slug(slug),ct)??throw new BusinessFault("PublishedProductNotFound",404);}
    public async Task<ProductView> SellableVariant(Guid tenant,Guid id,CancellationToken ct)
    {access.Machine(tenant,["Inventory","Pricing","Cart","Order","Manufacturing","Discovery"]);return await repository.PublishedVariant(id,ct)??throw new BusinessFault("VariantNotSellable",404);}
    public async Task<ProductView> PublishedVariantForStaff(Guid tenant,Guid id,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Staff,ct);return await repository.PublishedVariant(id,ct)??throw new BusinessFault("VariantNotSellable",404);}
    public async Task<Guid[]> PublicVariantIds(Guid tenant,Guid[] ids,CancellationToken ct)
    {await access.Public(tenant,ct);if(ids.Length is <1 or >100||ids.Any(id=>id==Guid.Empty)||ids.Distinct().Count()!=ids.Length)throw new BusinessFault("InvalidVariantQuery",400);return await repository.PublicVariantIds(ids,ct);}
    public async Task<object> Collection(Guid tenant,Guid? id,CollectionRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);return await repository.Transaction(async token=>
        {
            if(request.ProductIds is null||request.ProductIds.Length>200||request.ProductIds.Distinct().Count()!=request.ProductIds.Length)throw new BusinessFault("InvalidCollection");
            var value=id is null?new ProductCollection{TenantId=tenant}:(await repository.Collections(token)).SingleOrDefault(c=>c.Id==id)??throw new BusinessFault("CollectionNotFound",404);if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            value.Name=CatalogRules.Text(request.Name);value.Slug=CatalogRules.Slug(request.Slug);value.Archived=request.Archived;foreach(var row in value.Products)row.Active=false;
            var order=0;foreach(var productId in request.ProductIds){if(await repository.Product(productId,token)is null)throw new BusinessFault("CollectionProductNotFound",404);var row=value.Products.SingleOrDefault(p=>p.ProductId==productId);if(row is null){row=new(){TenantId=tenant,CollectionId=value.Id,ProductId=productId};value.Products.Add(row);}row.Active=true;row.Order=++order;}
            await repository.Commit("catalog.collection.saved",value.Id,null,token);return new{value.Id,value.Name,value.Slug,value.Archived,request.ProductIds,Version=Convert.ToBase64String(value.Version)};
        },ct);
    }
    public async Task<object> Import(Guid tenant,ImportRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);if(request.Key==Guid.Empty||request.Products is null||request.Products.Length is <1 or >100)throw new BusinessFault("InvalidImport",400);CatalogRules.Text(request.Source,80);
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Json(new{request.Source,request.Products}))));
        return await repository.Transaction(async token=>
        {
            var prior=await repository.Import(request.Key,token);if(prior is not null){if(prior.Hash!=hash)throw new BusinessFault("ImportKeyPayloadConflict");return Read<Guid[]>(prior.Result);}
            var ids=new List<Guid>();foreach(var product in request.Products){if(product.Version is not null||product.Variants.Any(v=>v.Id is not null)||product.Media.Any(m=>m.Id is not null))throw new BusinessFault("ImportCreatesDraftsOnly",400);ids.Add((await SaveCore(tenant,null,product,token)).Id);}
            repository.Add(new CatalogImport{TenantId=tenant,Id=request.Key,Source=request.Source,Hash=hash,Result=Json(ids)});await repository.Commit("catalog.products.imported",request.Key,null,token);return ids.ToArray();
        },ct);
    }
}
