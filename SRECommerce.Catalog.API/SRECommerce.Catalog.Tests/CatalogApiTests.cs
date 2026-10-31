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
    private static string Path(Guid tenant)=>$"/api/v1/tenants/{tenant}/catalog";
    private static string Public(Guid tenant)=>$"/api/v1/public/tenants/{tenant}/catalog";
    private static async Task<CatalogLookup> Category(CatalogBusiness business,string? slug=null)=>await Read<CatalogLookup>(await business.Client.PostAsJsonAsync(Path(business.Id)+"/categories",new CategoryRequest("Spices",slug??"spices",null)),HttpStatusCode.Created);
    private static ProductDraft Draft(Guid category,string? slug=null,string? sku=null)=>new("Whole spices",slug??"whole-spices",category,null,ProductKind.Simple,"Fresh whole spices","Spices","Whole spices",true,false,new(),new(),[new(null,sku??"SPICE-100",new())],[],[],[]);
    private static async Task<ProductView> Create(CatalogBusiness business,ProductDraft draft)=>await Read<ProductView>(await business.Client.PostAsJsonAsync(Path(business.Id)+"/products",draft),HttpStatusCode.Created);
    private static ProductDraft Edit(ProductView view)=>new(view.Name,view.Slug,view.CategoryId,view.BrandId,view.Kind,view.Description,view.SeoTitle,view.SeoDescription,view.Visible,view.B2BOnly,view.Attributes,view.Specifications,view.Variants,view.Media,view.BundleItems,view.Relationships,view.Version);
    private static async Task<ProductView> Publish(CatalogBusiness business,ProductView product)=>await Read<ProductView>(await business.Client.PostAsJsonAsync(Path(business.Id)+$"/products/{product.Id}/publish",new VersionRequest(product.Version!)));
    [Fact]
    public async Task Published_snapshot_is_isolated_from_drafts_and_archival_removes_it()
    {
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);var product=await Create(b,Draft(category.Id));using var anonymous=fixture.Factory.Client();
        Assert.Equal(0,(await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products"))!.Total);
        product=await Publish(b,product);var draft=await Read<ProductView>(await client.PutAsJsonAsync(Path(b.Id)+$"/products/{product.Id}",Edit(product) with{Name="Unpublished change"}));
        var published=(await anonymous.GetFromJsonAsync<ProductView>(Public(b.Id)+"/products/whole-spices"))!;Assert.Equal("Whole spices",published.Name);Assert.Null(published.Version);
        Assert.Equal(0,(await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products?search=Unpublished"))!.Total);
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync(Path(b.Id)+$"/products/{product.Id}/archive",new VersionRequest(draft.Version!))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await anonymous.GetAsync(Public(b.Id)+"/products/whole-spices")).StatusCode);
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
        var b=await fixture.Business();using var client=b.Client;var category=await Category(b);var component=await Publish(b,await Create(b,Draft(category.Id)));
        var bundleDraft=Draft(category.Id,"spice-bundle","BUNDLE-1") with{Kind=ProductKind.Bundle,BundleItems=[new(component.Variants[0].Id!.Value,2)]};var bundle=await Publish(b,await Create(b,bundleDraft));
        await Read<JsonElement>(await client.PostAsJsonAsync(Path(b.Id)+"/collections",new CollectionRequest("Bundles","bundles",[bundle.Id])),HttpStatusCode.Created);
        using var anonymous=fixture.Factory.Client();var page=(await anonymous.GetFromJsonAsync<CatalogPage<ProductView>>(Public(b.Id)+"/products?collection=bundles"))!;Assert.Single(page.Items);Assert.Equal(bundle.Id,page.Items.Single().Id);
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
    public async Task Migrations_health_openapi_and_machine_separation_work()
    {
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();await db.Database.MigrateAsync();Assert.Empty(await db.Database.GetPendingMigrationsAsync());using var client=fixture.Factory.Client();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(Path(Guid.NewGuid())+"/products")).StatusCode);
        var business=await fixture.Business();using var human=business.Client;Assert.Equal(HttpStatusCode.Unauthorized,(await human.GetAsync($"/internal/v1/tenants/{business.Id}/catalog/variants/{Guid.NewGuid()}")).StatusCode);
    }
}
