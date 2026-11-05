using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Domain;
using SRECommerce.Catalog.Infrastructure;
using Xunit;
using static SRECommerce.Catalog.Tests.CatalogFixture;
namespace SRECommerce.Catalog.Tests;
public sealed class CatalogApiTests(CatalogFixture fixture):IClassFixture<CatalogFixture>
{
    [Fact]
    public async Task Bound_fields_cannot_be_archived_and_unbinding_preserves_the_current_publication()
    {
        var business=await fixture.Business();using var owner=business.Client;using var anonymous=fixture.Factory.Client();var category=await Category(business);
        var field=await Read<JsonElement>(await owner.PostAsJsonAsync(Path(business.Id)+"/attributes",new AttributeRequest("origin","Origin",AttributeDataType.Text,[])),HttpStatusCode.Created);
        var id=field.GetProperty("id").GetGuid();var version=field.GetProperty("version").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent,(await owner.PutAsJsonAsync(Path(business.Id)+$"/categories/{category.Id}/attributes?version={Uri.EscapeDataString(category.Version)}",new[]{new AttributeBinding(id,false)})).StatusCode);
        var product=await Publish(business,await Create(business,Draft(category.Id) with{Attributes=new(){["origin"]=JsonSerializer.SerializeToElement("Local")}}));
        var archive=new AttributeRequest("origin","Origin",AttributeDataType.Text,[],null,version,true);
        var denied=await owner.PutAsJsonAsync(Path(business.Id)+"/attributes/"+id,archive);Assert.Equal(HttpStatusCode.Conflict,denied.StatusCode);
        var lookups=await Read<JsonElement>(await owner.GetAsync(Path(business.Id)+"/lookups"));var categoryVersion=lookups.GetProperty("categories").EnumerateArray().Single(row=>row.GetProperty("id").GetGuid()==category.Id).GetProperty("version").GetString()!;
        Assert.Equal(HttpStatusCode.NoContent,(await owner.PutAsJsonAsync(Path(business.Id)+$"/categories/{category.Id}/attributes?version={Uri.EscapeDataString(categoryVersion)}",Array.Empty<AttributeBinding>())).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await owner.PutAsJsonAsync(Path(business.Id)+"/attributes/"+id,archive)).StatusCode);
        var published=(await anonymous.GetFromJsonAsync<ProductView>(Public(business.Id)+"/products/"+product.Slug))!;Assert.Equal("Local",published.Attributes["origin"].GetString());
        Assert.Equal(HttpStatusCode.OK,(await owner.PutAsJsonAsync(Path(business.Id)+"/products/"+product.Id,Edit(product) with{Attributes=new()})).StatusCode);
    }
    [Fact]
    public async Task Product_choice_batches_are_scoped_to_staff_and_never_reveal_another_tenants_drafts()
    {
        var a=await fixture.Business();var b=await fixture.Business();using var owner=a.Client;using var otherOwner=b.Client;using var anonymous=fixture.Factory.Client();
        var aCategory=await Category(a);var bCategory=await Category(b);
        var own=await Create(a,Draft(aCategory.Id));var foreign=await Create(b,Draft(bCategory.Id));var path=Path(a.Id)+"/product-choices";
        var choice=Assert.Single(await Read<ProductChoice[]>(await owner.PostAsJsonAsync(path,new ProductChoicesRequest([own.Id,foreign.Id]))));
        Assert.Equal(own.Id,choice.Id);Assert.Equal(ProductStatus.Draft,choice.Status);Assert.Equal(0,choice.PublishedRevision);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(path,new ProductChoicesRequest([own.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await otherOwner.PostAsJsonAsync(path,new ProductChoicesRequest([own.Id]))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsJsonAsync(path,new ProductChoicesRequest([own.Id,own.Id]))).StatusCode);
        Assert.Empty(await Read<ProductChoice[]>(await owner.PostAsJsonAsync(path,new ProductChoicesRequest([]))));
    }
    [Fact]
    public async Task Member_catalog_returns_only_published_audience_eligible_snapshots()
    {
        var b=await fixture.Business();using var owner=b.Client;using var anonymous=fixture.Factory.Client();
        var category=await Category(b);var product=await Publish(b,await Create(b,Draft(category.Id) with{B2BOnly=true}));
        var buyer=Path(b.Id)+"/buyer";
        Assert.Empty((await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products"))!.Items);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(buyer+"/products")).StatusCode);
        var published=Assert.Single((await owner.GetFromJsonAsync<CatalogPage<ProductView>>(buyer+"/products"))!.Items);
        Assert.Equal(product.Id,published.Id);Assert.Null(published.Version);Assert.True(published.B2BOnly);
        Assert.Equal(product.Id,(await owner.GetFromJsonAsync<ProductView>(buyer+"/products/"+product.Slug))!.Id);
        Assert.Single((await owner.GetFromJsonAsync<ProductView[]>(buyer+"/published-products?ids="+product.Id))!);
        _=await Read<ProductView>(await owner.PutAsJsonAsync(Path(b.Id)+"/products/"+product.Id,Edit(product) with{Name="Unpublished private edit"}));
        Assert.Equal(product.Name,(await owner.GetFromJsonAsync<ProductView>(buyer+"/products/"+product.Slug))!.Name);
    }
    [Fact]
    public async Task Uploaded_images_are_bounded_and_only_current_public_product_references_are_served()
    {
        var b=await fixture.Business();using var owner=b.Client;using var anonymous=fixture.Factory.Client();
        // A real 1x1 PNG, never an external URL or client-controlled file path.
        var png=Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a0wAAAABJRU5ErkJggg==");
        using var upload=new MultipartFormDataContent();upload.Add(new ByteArrayContent(png),"file","../../ignored.png");
        var asset=await Read<MediaAssetView>(await owner.PostAsync(Path(b.Id)+"/media-assets",upload),HttpStatusCode.Created);
        Assert.Equal(1,asset.Width);Assert.Equal(1,asset.Height);
        var publicPath=asset.Url.Replace("/commerce/catalog/public/","/api/v1/public/");
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(publicPath)).StatusCode);
        var category=await Category(b);var product=await Create(b,Draft(category.Id) with{Media=[new(null,asset.Url,"Test image",1)]});
        product=await Publish(b,product);using var image=await anonymous.GetAsync(publicPath);
        Assert.Equal(HttpStatusCode.OK,image.StatusCode);Assert.Equal("image/png",image.Content.Headers.ContentType!.MediaType);Assert.Equal(png,await image.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff",image.Headers.GetValues("X-Content-Type-Options").Single());
        category=await Read<CatalogLookup>(await owner.PutAsJsonAsync(Path(b.Id)+"/categories/"+category.Id,new CategoryRequest(category.Name,category.Slug,null,category.Version,true)));
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(publicPath)).StatusCode);
        _=await Read<CatalogLookup>(await owner.PutAsJsonAsync(Path(b.Id)+"/categories/"+category.Id,new CategoryRequest(category.Name,category.Slug,null,category.Version,false)));
        Assert.Equal(HttpStatusCode.OK,(await anonymous.GetAsync(publicPath)).StatusCode);
        product=await Read<ProductView>(await owner.PutAsJsonAsync(Path(b.Id)+"/products/"+product.Id,Edit(product) with{B2BOnly=true}));
        product=await Publish(b,product);
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(publicPath)).StatusCode);
        var privatePath=Path(b.Id)+"/buyer/media/"+asset.Id;
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(privatePath)).StatusCode);
        using var privateImage=await owner.GetAsync(privatePath);Assert.Equal(HttpStatusCode.OK,privateImage.StatusCode);Assert.Equal(png,await privateImage.Content.ReadAsByteArrayAsync());
        var other=await fixture.Business();using var otherOwner=other.Client;var otherCategory=await Category(other);
        Assert.Equal(HttpStatusCode.BadRequest,(await otherOwner.PostAsJsonAsync(Path(other.Id)+"/products",Draft(otherCategory.Id) with{Media=[new(null,asset.Url,"Foreign asset",1)]})).StatusCode);
        using var invalid=new MultipartFormDataContent();invalid.Add(new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(new string('x',70))),"file","script.png");
        Assert.Equal(HttpStatusCode.BadRequest,(await owner.PostAsync(Path(b.Id)+"/media-assets",invalid)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await owner.PostAsJsonAsync(Path(b.Id)+$"/products/{product.Id}/archive",new VersionRequest(product.Version!))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(publicPath)).StatusCode);
    }
    private static string Path(Guid tenant)=>$"/api/v1/tenants/{tenant}/catalog";
    private static string Public(Guid tenant)=>$"/api/v1/public/tenants/{tenant}/catalog";
    private static async Task<CatalogLookup> Category(CatalogBusiness business,string? slug=null)=>await Read<CatalogLookup>(await business.Client.PostAsJsonAsync(Path(business.Id)+"/categories",new CategoryRequest("Spices",slug??"spices",null)),HttpStatusCode.Created);
    private static ProductDraft Draft(Guid category,string? slug=null,string? sku=null)=>new("Whole spices",slug??"whole-spices",category,null,ProductKind.Simple,"Fresh whole spices","Spices","Whole spices",true,false,new(),new(),[new(null,sku??"SPICE-100",new())],[],[],[]);
    private static async Task<ProductView> Create(CatalogBusiness business,ProductDraft draft)=>await Read<ProductView>(await business.Client.PostAsJsonAsync(Path(business.Id)+"/products",draft),HttpStatusCode.Created);
    private static ProductDraft Edit(ProductView view)=>new(view.Name,view.Slug,view.CategoryId,view.BrandId,view.Kind,view.Description,view.SeoTitle,view.SeoDescription,view.Visible,view.B2BOnly,view.Attributes,view.Specifications,view.Variants,view.Media,view.BundleItems,view.Relationships,view.Version);
    private static async Task<ProductView> Publish(CatalogBusiness business,ProductView product)=>await Read<ProductView>(await business.Client.PostAsJsonAsync(Path(business.Id)+$"/products/{product.Id}/publish",new VersionRequest(product.Version!)));
    [Fact]
    public async Task Publication_batches_rollback_partial_writes_and_replay_without_new_revisions()
    {
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);var a=await Create(b,Draft(category.Id));var c=await Create(b,Draft(category.Id,"second","SECOND"));var path=Path(b.Id);
        var request=new PublishBatchRequest(Guid.NewGuid(),"verified-source",[new(a.Id,a.Version!),new(c.Id,Convert.ToBase64String(new byte[8]))]);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/publish-batch",request)).StatusCode);
        using var anonymous=fixture.Factory.Client();Assert.Equal(0,(await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products"))!.Total);
        request=request with{Products=[new(a.Id,a.Version!),new(c.Id,c.Version!)]};var ids=await Read<Guid[]>(await client.PostAsJsonAsync(path+"/publish-batch",request));Assert.Equal(2,ids.Length);
        Assert.Equal(ids,await Read<Guid[]>(await client.PostAsJsonAsync(path+"/publish-batch",request)));
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/publish-batch",request with{Source="changed"})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(path+"/published-skus?ids="+a.Variants[0].Id)).StatusCode);
        var skus=(await client.GetFromJsonAsync<SRECommerce.BuildingBlocks.Contracts.PublishedSku[]>(path+"/published-skus?ids="+a.Variants[0].Id+"&ids="+c.Variants[0].Id))!;Assert.Equal(2,skus.Length);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();Assert.Equal(2,await db.Publications.IgnoreQueryFilters().CountAsync(p=>p.TenantId==b.Id));
    }
    [Fact]
    public async Task Published_snapshot_is_isolated_from_drafts_and_archival_removes_it()
    {
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);var product=await Create(b,Draft(category.Id));using var anonymous=fixture.Factory.Client();
        Assert.Equal(0,(await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products"))!.Total);
        product=await Publish(b,product);var draft=await Read<ProductView>(await client.PutAsJsonAsync(Path(b.Id)+$"/products/{product.Id}",Edit(product) with{Name="Unpublished change"}));
        var published=(await anonymous.GetFromJsonAsync<ProductView>(Public(b.Id)+"/products/whole-spices"))!;Assert.Equal("Whole spices",published.Name);Assert.Null(published.Version);
        var byProduct=Assert.Single((await anonymous.GetFromJsonAsync<ProductView[]>(Public(b.Id)+"/published-products?ids="+product.Id))!);
        Assert.Equal(published.PublishedRevision,byProduct.PublishedRevision);
        Assert.Empty((await anonymous.GetFromJsonAsync<ProductView[]>(Public(b.Id)+"/published-products?ids="+product.Variants[0].Id))!);
        var skuResult=Assert.Single((await anonymous.GetFromJsonAsync<ProductView[]>(Public(b.Id)+"/shopping-products?ids="+product.Variants[0].Id))!);
        Assert.Equal(product.Id,skuResult.Id);
        Assert.Equal(0,(await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products?search=Unpublished"))!.Total);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync(Path(b.Id)+$"/products/{product.Id}/archive",new VersionRequest(draft.Version!))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(Public(b.Id)+"/products/whole-spices")).StatusCode);
        Assert.Empty((await anonymous.GetFromJsonAsync<ProductView[]>(Public(b.Id)+"/published-products?ids="+product.Id))!);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();Assert.Equal(2,await db.Outbox.IgnoreQueryFilters().CountAsync(x=>x.TenantId==b.Id));Assert.Equal(1,await db.Publications.IgnoreQueryFilters().CountAsync(x=>x.TenantId==b.Id));
    }
    [Fact]
    public async Task Inherited_required_attributes_and_category_cycles_are_enforced()
    {
        var b=await fixture.Business();using var client=b.Client;var root=await Category(b);var child=await Read<CatalogLookup>(await client.PostAsJsonAsync(Path(b.Id)+"/categories",new CategoryRequest("Masala","masala",root.Id)),HttpStatusCode.Created);
        var definition=await Read<JsonElement>(await client.PostAsJsonAsync(Path(b.Id)+"/attributes",new AttributeRequest("weight","Weight",AttributeDataType.Number,[],"g")),HttpStatusCode.Created);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PutAsJsonAsync(Path(b.Id)+$"/categories/{root.Id}/attributes?version={Uri.EscapeDataString(root.Version)}",new[]{new AttributeBinding(definition.GetProperty("id").GetGuid(),true)})).StatusCode);
        var lookups=await client.GetFromJsonAsync<JsonElement>(Path(b.Id)+"/lookups");var refreshed=lookups.GetProperty("categories").EnumerateArray().Single(c=>c.GetProperty("id").GetGuid()==root.Id).GetProperty("version").GetString()!;
        Assert.NotEqual(root.Version,refreshed);Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Path(b.Id)+$"/categories/{root.Id}",new CategoryRequest("Loop","spices",child.Id,refreshed))).StatusCode);
        var product=await Create(b,Draft(child.Id));Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Path(b.Id)+$"/products/{product.Id}/publish",new VersionRequest(product.Version!))).StatusCode);
        product=await Read<ProductView>(await client.PutAsJsonAsync(Path(b.Id)+$"/products/{product.Id}",Edit(product) with{Attributes=new(){["weight"]=JsonSerializer.SerializeToElement(100)}}));await Publish(b,product);
    }
    [Fact]
    public async Task Cross_tenant_ids_headers_and_same_tenant_duplicate_skus_cannot_corrupt_data()
    {
        var a=await fixture.Business();var b=await fixture.Business();using(a.Client)using(b.Client)
        {
            var ca=await Category(a);var cb=await Category(b);var first=await Create(a,Draft(ca.Id));await Create(b,Draft(cb.Id)); // SKU names may repeat across tenants.
            a.Client.DefaultRequestHeaders.Add("X-Tenant-ID",b.Id.ToString());Assert.Equal(HttpStatusCode.Forbidden,(await a.Client.GetAsync(Path(b.Id)+"/products")).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await a.Client.PostAsJsonAsync(Path(a.Id)+"/products",Draft(ca.Id,"other"))).StatusCode);
            Assert.Equal(HttpStatusCode.Conflict,(await a.Client.PostAsJsonAsync(Path(a.Id)+"/products",Draft(cb.Id,"foreign","NEW-SKU"))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await b.Client.GetAsync(Path(b.Id)+$"/products/{first.Id}")).StatusCode);
            using var scope=fixture.Factory.Services.CreateScope();Assert.Empty(await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Products.ToListAsync());
        }
    }
    [Fact]
    public async Task Concurrent_updates_have_one_winner_and_published_sku_identity_is_stable()
    {
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);var product=await Create(b,Draft(category.Id));
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(i=>client.PutAsJsonAsync(Path(b.Id)+$"/products/{product.Id}",Edit(product) with{Name="Concurrent "+i})));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        product=(await client.GetFromJsonAsync<ProductView>(Path(b.Id)+$"/products/{product.Id}"))!;product=await Publish(b,product);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Path(b.Id)+$"/products/{product.Id}",Edit(product) with{Variants=[product.Variants[0] with{Sku="REASSIGNED"}]})).StatusCode);
    }
    [Fact]
    public async Task Collections_filter_published_products_and_bundle_publication_requires_live_components()
    {
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);var component=await Publish(b,await Create(b,Draft(category.Id) with{Name="Alpha component"}));
        var bundleDraft=Draft(category.Id,"spice-bundle","BUNDLE-1") with{Name="Zulu bundle",Kind=ProductKind.Bundle,BundleItems=[new(component.Variants[0].Id!.Value,2)]};var bundle=await Publish(b,await Create(b,bundleDraft));
        await Read<JsonElement>(await client.PostAsJsonAsync(Path(b.Id)+"/collections",new CollectionRequest("Bundles","bundles",[bundle.Id,component.Id])),HttpStatusCode.Created);
        using var anonymous=fixture.Factory.Client();var page=(await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products?collection=bundles"))!;Assert.Equal(new[]{bundle.Id,component.Id},page.Items.Select(p=>p.Id).ToArray());
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Path(b.Id)+"/products",Draft(category.Id,"nested","BUNDLE-2") with{Kind=ProductKind.Bundle,BundleItems=[new(bundle.Variants[0].Id!.Value,1)]})).StatusCode);
    }
    [Fact]
    public async Task Import_is_atomic_idempotent_and_conflicting_key_does_not_overwrite()
    {
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);var request=new ImportRequest(Guid.NewGuid(),"SQL test import",[Draft(category.Id)]);
        var first=await Read<Guid[]>(await client.PostAsJsonAsync(Path(b.Id)+"/import",request));var repeat=await Read<Guid[]>(await client.PostAsJsonAsync(Path(b.Id)+"/import",request));Assert.Equal(first,repeat);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Path(b.Id)+"/import",request with{Products=[Draft(category.Id,"changed","CHANGED")]})).StatusCode);
        var bad=new ImportRequest(Guid.NewGuid(),"Atomic invalid import",[Draft(category.Id,"valid","VALID"),Draft(Guid.NewGuid(),"invalid","INVALID")]);Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Path(b.Id)+"/import",bad)).StatusCode);
        var all=(await client.GetFromJsonAsync<CatalogPage<ProductView>>(Path(b.Id)+"/products"))!;Assert.Equal(1,all.Total);
    }
    [Fact]
    public async Task Identity_and_tenant_outages_fail_closed_and_unknown_json_is_rejected()
    {
        var b=await fixture.Business();using var client=b.Client;
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Path(b.Id)+"/categories",new{name="Injected",slug="inject",parentId=(Guid?)null,tenantId=Guid.NewGuid()})).StatusCode);
        try{fixture.Factory.IdentityOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(Path(b.Id)+"/products")).StatusCode);fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=true;using var anonymous=fixture.Factory.Client();Assert.Equal(HttpStatusCode.ServiceUnavailable,(await anonymous.GetAsync(Public(b.Id)+"/products")).StatusCode);}
        finally{fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=false;}
    }
    [Fact]
    public async Task Staff_variant_lookup_uses_the_published_snapshot_and_hides_new_draft_skus()
    {
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);
        var product=await Publish(b,await Create(b,Draft(category.Id) with{Kind=ProductKind.Variable,Variants=[new(null,"SIZE-S",new(){["size"]="S"})]}));
        var first=product.Variants[0];var draft=await Read<ProductView>(await client.PutAsJsonAsync(Path(b.Id)+$"/products/{product.Id}",Edit(product) with{Variants=[first,new(null,"SIZE-M",new(){["size"]="M"})]}));
        var added=draft.Variants.Single(v=>v.Sku=="SIZE-M");Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync(Path(b.Id)+$"/variants/{added.Id}")).StatusCode);
        var published=await Read<ProductView>(await client.GetAsync(Path(b.Id)+$"/variants/{first.Id}"));Assert.Single(published.Variants);
        await Publish(b,draft);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync(Path(b.Id)+$"/variants/{added.Id}")).StatusCode);
    }
    [Fact]
    public async Task Migrations_health_openapi_and_machine_separation_work()
    {
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();await db.Database.MigrateAsync();Assert.Empty(await db.Database.GetPendingMigrationsAsync());using var client=fixture.Factory.Client();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(Path(Guid.NewGuid())+"/products")).StatusCode);
        var business=await fixture.Business();using var human=business.Client;Assert.Equal(HttpStatusCode.Unauthorized,(await human.GetAsync($"/internal/v1/tenants/{business.Id}/catalog/variants/{Guid.NewGuid()}")).StatusCode);
    }
}
