using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Identity.Tests;
using SRECommerce.Tenant.Application;
using SRECommerce.Theme.Application;
using SRECommerce.Theme.Domain;
using SRECommerce.Theme.Infrastructure;
using Xunit;
using static SRECommerce.Theme.Tests.ThemeFixture;

namespace SRECommerce.Theme.Tests;
public sealed class ThemeApiTests(ThemeFixture fixture):IClassFixture<ThemeFixture>
{
    private static string Path(Guid tenant)=>$"/api/v1/tenants/{tenant}/themes";
    private static async Task<ThemeView> Create(ThemeBusiness business)=>await Read<ThemeView>(await business.Client.PostAsJsonAsync(Path(business.Id),new CreateTheme("Theme-"+Guid.NewGuid().ToString("N"),"spices")),HttpStatusCode.Created);
    [Fact]
    public async Task Publish_versions_rollback_and_public_snapshot_are_durable_and_atomic()
    {
        var business=await fixture.Business();using var client=business.Client;var draft=await Create(business);
        var first=await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+$"/{draft.Id}/publish",new PublishTheme(draft.Version,null)));
        var edited=draft.Document with{Desktop=draft.Document.Desktop with{Radius=16}};
        draft=await Read<ThemeView>(await client.PutAsJsonAsync(Path(business.Id)+$"/{draft.Id}/draft",new SaveTheme(first.ThemeVersion,edited)));
        var second=await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+$"/{draft.Id}/publish",new PublishTheme(draft.Version,first.PublicationVersion)));
        Assert.Equal(2,second.Sequence);Assert.Equal(2,(await client.GetFromJsonAsync<VersionView[]>(Path(business.Id)+$"/{draft.Id}/versions"))!.Length);
        using var anonymous=fixture.Factory.Client();var published=await anonymous.GetFromJsonAsync<PublicTheme>($"/api/v1/public/tenants/{business.Id}/theme");Assert.Equal(16,published!.Document.Desktop.Radius);
        var rollback=await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+"/rollback",new RollbackTheme(first.VersionId,second.PublicationVersion)));Assert.Equal(3,rollback.Sequence);
        published=await anonymous.GetFromJsonAsync<PublicTheme>($"/api/v1/public/tenants/{business.Id}/theme");Assert.Equal(8,published!.Document.Desktop.Radius);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ThemeDbContext>();
        Assert.Equal(3,await db.Outbox.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));Assert.Equal(3,await db.Publications.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));
        Assert.Equal(2,await db.Versions.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));
    }
    [Fact]
    public async Task Concurrent_first_publish_has_one_winner_and_stale_draft_cannot_overwrite()
    {
        var business=await fixture.Business();using var client=business.Client;var draft=await Create(business);
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(_=>client.PostAsJsonAsync(Path(business.Id)+$"/{draft.Id}/publish",new PublishTheme(draft.Version,null))));
        Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.OK);Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PutAsJsonAsync(Path(business.Id)+$"/{draft.Id}/draft",new SaveTheme(draft.Version,draft.Document))).StatusCode);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ThemeDbContext>();Assert.Equal(1,await db.Outbox.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));
    }
    [Fact]
    public async Task Tenant_and_header_spoofing_cannot_read_or_publish_another_draft()
    {
        var a=await fixture.Business();var b=await fixture.Business();using(a.Client)using(b.Client)
        {
            var draft=await Create(b);a.Client.DefaultRequestHeaders.Add("X-Tenant-ID",b.Id.ToString());
            Assert.Equal(HttpStatusCode.Forbidden,(await a.Client.GetAsync(Path(b.Id))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await a.Client.PostAsJsonAsync(Path(a.Id)+$"/{draft.Id}/publish",new PublishTheme(draft.Version,null))).StatusCode);
            using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ThemeDbContext>();Assert.Empty(await db.Themes.ToListAsync());
        }
    }
    [Fact]
    public async Task Invalid_schema_contrast_and_unknown_properties_leave_no_publication()
    {
        var business=await fixture.Business();using var client=business.Client;var draft=await Create(business);var invalid=ThemeRules.Default("spices");invalid.Light.Colors["text"]="#ffffff";
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync(Path(business.Id)+$"/{draft.Id}/draft",new SaveTheme(draft.Version,invalid))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Path(business.Id)+"/preview",new{schemaVersion=1,light=(object?)null,dark=(object?)null,desktop=(object?)null,mobile=(object?)null,defaultMode="light"})).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync(Path(business.Id),new{name="Injected",preset="neutral",css="url(javascript:bad)"})).StatusCode);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ThemeDbContext>();Assert.Equal(0,await db.Outbox.IgnoreQueryFilters().CountAsync(x=>x.TenantId==business.Id));
    }
    [Fact]
    public async Task Live_dependency_failure_fails_closed_but_last_valid_public_theme_survives()
    {
        var business=await fixture.Business();using var client=business.Client;var draft=await Create(business);await Read<PublicationView>(await client.PostAsJsonAsync(Path(business.Id)+$"/{draft.Id}/publish",new PublishTheme(draft.Version,null)));
        using var anonymous=fixture.Factory.Client();
        try
        {
            fixture.Factory.IdentityOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(Path(business.Id))).StatusCode);
            anonymous.DefaultRequestHeaders.Authorization=new("Bearer",business.Token);
            var cached=await anonymous.GetFromJsonAsync<PublicTheme>($"/api/v1/public/tenants/{business.Id}/theme");Assert.Equal("last-valid-snapshot",cached!.Source);
            fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync(Path(business.Id))).StatusCode);
            var fallback=await anonymous.GetFromJsonAsync<PublicTheme>($"/api/v1/public/tenants/{Guid.NewGuid()}/theme?businessType=grocery");Assert.Equal("business-default",fallback!.Source);ThemeRules.Validate(fallback.Document);
        }
        finally{fixture.Factory.IdentityOffline=false;fixture.Factory.TenantOffline=false;}
    }
    [Fact]
    public async Task Disabled_module_and_revoked_session_cannot_write()
    {
        var business=await fixture.Business();using var client=business.Client;
        using var tenantClient=fixture.Tenant.Factory.Client(business.Token);var tenant=(await tenantClient.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{business.Id}"))!;
        await Read<TenantView>(await tenantClient.PutAsJsonAsync($"/api/v1/tenants/{business.Id}/modules/themes",new ModuleRequest(tenant.Version,false)));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.GetAsync(Path(business.Id))).StatusCode);
        var identityTokens=await Protocol.SignIn(fixture.Tenant.Identity.Factory,business.Email,business.Password,business.Id,scope:Scopes);using var revoked=fixture.Factory.Client(identityTokens.Access);
        Assert.Equal(HttpStatusCode.NoContent,(await identityTokens.Client.PostAsync("/api/v1/logout",null)).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await revoked.GetAsync(Path(business.Id))).StatusCode);
    }
    [Fact]
    public async Task Sql_outage_uses_bundled_theme_independent_of_bearer_and_registry()
    {
        using var unavailable=new ThemeFactory(fixture.Tenant){SqlOffline=true,TenantOffline=true,IdentityOffline=true};using var client=unavailable.Client("unused-bearer");
        var response=await client.GetAsync($"/api/v1/public/tenants/{Guid.NewGuid()}/theme?businessType=clothing&mode=dark");
        var value=await Read<PublicTheme>(response);Assert.Equal("business-default",value.Source);ThemeRules.Validate(value.Document);Assert.NotEmpty(value.Variables);
        Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync("/health/ready")).StatusCode);
    }
    [Fact]
    public async Task Migrations_are_reentrant_and_health_and_openapi_are_available()
    {
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ThemeDbContext>();await db.Database.MigrateAsync();Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        using var client=fixture.Factory.Client();Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(Path(Guid.NewGuid()))).StatusCode);
    }
}
