using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.Storefront.Application;
using SRECommerce.Storefront.Domain;
using SRECommerce.Storefront.Infrastructure;
using Xunit;
using static SRECommerce.Storefront.Tests.StorefrontFixture;

namespace SRECommerce.Storefront.Tests;
public sealed class StorefrontApiTests(StorefrontFixture fixture):IClassFixture<StorefrontFixture>
{
    private static string Path(Guid tenant)=>$"/api/v1/tenants/{tenant}/pages";
    private static async Task<PageView> Create(StorefrontBusiness business,string slug="home")=>await Read<PageView>(await business.Client.PostAsJsonAsync(Path(business.Id),new CreatePage(slug,"Test page","spices")),HttpStatusCode.Created);
    [Fact]
    public async Task Draft_publish_device_visibility_version_and_rollback_work_with_atomic_outbox()
    {
        var business=await fixture.Business();using var client=business.Client;var page=await Create(business);
        using var publicClient=fixture.Factory.Client();var fallback=(await publicClient.GetFromJsonAsync<PublicLayout>($"/api/v1/public/tenants/{business.Id}/pages/home"))!;Assert.Equal(0,fallback.Sequence);
        var first=await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+$"/{page.Id}/publish",new PublishPage(page.Version,null)));
        var sections=page.Document.Sections.ToArray();sections[0]=sections[0] with{Mobile=new(false,4,1)};var edited=page.Document with{Sections=sections,Product=page.Document.Product with{Reviews=false}};
        page=await Read<PageView>(await client.PutAsJsonAsync(Path(business.Id)+$"/{page.Id}/draft",new SavePage(first.PageVersion,"Edited draft",edited)));
        var stillFirst=(await publicClient.GetFromJsonAsync<PublicLayout>($"/api/v1/public/tenants/{business.Id}/pages/home"))!;Assert.True(stillFirst.Document.Product.Reviews); // Drafts are never public.
        var second=await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+$"/{page.Id}/publish",new PublishPage(page.Version,first.PublicationVersion)));
        var published=(await publicClient.GetFromJsonAsync<PublicLayout>($"/api/v1/public/tenants/{business.Id}/pages/home"))!;Assert.False(published.Document.Product.Reviews);Assert.False(published.Document.Sections[0].Mobile.Visible);
        var rollback=await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+$"/{page.Id}/rollback",new RollbackPage(first.VersionId,second.PublicationVersion)));Assert.Equal(3,rollback.Sequence);
        var restored=(await publicClient.GetFromJsonAsync<PublicLayout>($"/api/v1/public/tenants/{business.Id}/pages/home"))!;Assert.True(restored.Document.Product.Reviews);
        Assert.Equal(2,(await client.GetFromJsonAsync<VersionView[]>(Path(business.Id)+$"/{page.Id}/versions"))!.Length);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<StorefrontDbContext>();Assert.Equal(3,await db.Outbox.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));Assert.Equal(3,await db.Publications.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));
    }
    [Fact]
    public async Task Concurrent_publication_and_stale_draft_cannot_lose_updates()
    {
        var business=await fixture.Business();using var client=business.Client;var page=await Create(business);
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>client.PostAsJsonAsync(Path(business.Id)+$"/{page.Id}/publish",new PublishPage(page.Version,null))));
        Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Path(business.Id)+$"/{page.Id}/draft",new SavePage(page.Version,"Stale",page.Document))).StatusCode);
    }
    [Fact]
    public async Task Tenant_headers_and_other_page_versions_cannot_redirect_a_publication()
    {
        var a=await fixture.Business();var b=await fixture.Business();using(a.Client)using(b.Client)
        {
            var first=await Create(a);var second=await Create(a,"sale");var foreign=await Create(b);a.Client.DefaultRequestHeaders.Add("X-Tenant-ID",b.Id.ToString());
            Assert.Equal(HttpStatusCode.Forbidden,(await a.Client.GetAsync(Path(b.Id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await a.Client.PostAsJsonAsync(Path(a.Id)+$"/{foreign.Id}/publish",new PublishPage(foreign.Version,null))).StatusCode);
            var publication=await Read<PublicationView>(await a.Client.PostAsJsonAsync(Path(a.Id)+$"/{first.Id}/publish",new PublishPage(first.Version,null)));
            Assert.Equal(HttpStatusCode.NotFound,(await a.Client.PostAsJsonAsync(Path(a.Id)+$"/{second.Id}/rollback",new RollbackPage(publication.VersionId,publication.PublicationVersion))).StatusCode);
            using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<StorefrontDbContext>();Assert.Empty(await db.Pages.ToListAsync());
        }
    }
    [Fact]
    public async Task Unsafe_banners_unknown_json_and_duplicate_slugs_are_rejected_without_outbox()
    {
        var business=await fixture.Business();using var client=business.Client;var page=await Create(business);
        var invalid=page.Document with{Banners=[new(Guid.NewGuid(),"Unsafe","javascript:alert(1)","Image","/products",new(true,1,1),new(true,1,1))]};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(Path(business.Id)+$"/{page.Id}/draft",new SavePage(page.Version,"Title",invalid))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Path(business.Id),new{slug="other",title="Injected",template="neutral",html="<script>bad</script>"})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(Path(business.Id),new CreatePage("home","Duplicate"))).StatusCode);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<StorefrontDbContext>();Assert.Equal(0,await db.Outbox.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));
    }
    [Fact]
    public async Task Authorization_outage_fails_closed_while_public_layout_remains_available()
    {
        var business=await fixture.Business();using var client=business.Client;var page=await Create(business);await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+$"/{page.Id}/publish",new PublishPage(page.Version,null)));
        using var publicClient=fixture.Factory.Client(business.Token);
        try
        {
            fixture.Factory.IdentityOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(Path(business.Id))).StatusCode);
            Assert.Equal("last-valid-snapshot",(await publicClient.GetFromJsonAsync<PublicLayout>($"/api/v1/public/tenants/{business.Id}/pages/home"))!.Source);
            fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(Path(business.Id))).StatusCode);
            Assert.Equal("business-default",(await publicClient.GetFromJsonAsync<PublicLayout>($"/api/v1/public/tenants/{Guid.NewGuid()}/pages/home?businessType=clothing"))!.Source);
        }
        finally{fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=false;}
    }
    [Fact]
    public async Task Sql_registry_and_identity_outage_returns_valid_business_template()
    {
        using var unavailable=new StorefrontFactory(fixture.Tenant){SqlOffline=true,TenantOffline=true,IdentityOffline=true};using var client=unavailable.Client("unused-bearer");
        var value=await Read<PublicLayout>(await client.GetAsync($"/api/v1/public/tenants/{Guid.NewGuid()}/pages/home?businessType=grocery"));Assert.Equal("business-default",value.Source);LayoutRules.Validate(value.Document);
    }
    [Fact]
    public async Task Reapplied_migrations_health_and_openapi_work()
    {
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<StorefrontDbContext>();await db.Database.MigrateAsync();Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        using var client=fixture.Factory.Client();Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(Path(Guid.NewGuid()))).StatusCode);
    }
}
