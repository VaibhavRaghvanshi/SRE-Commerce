using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Tests;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;
using SRECommerce.Tenant.Infrastructure;
using Xunit;
using RegistrationRequest=SRECommerce.Tenant.Application.RegistrationRequest;

namespace SRECommerce.Tenant.Tests;

public sealed class TenantTests(TenantFixture fixture) : IClassFixture<TenantFixture>
{
    private TenantFactory Factory=>fixture.Factory;
    private static async Task<T> Read<T>(HttpResponseMessage response,HttpStatusCode status=HttpStatusCode.OK)
    {Assert.Equal(status,response.StatusCode);return (await response.Content.ReadFromJsonAsync<T>())!;}
    private async Task<TenantView> Register(HttpClient client,Guid? key=null,string? slug=null,string type="spices",string? custom=null)
    {
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/tenants"){Content=JsonContent.Create(new RegistrationRequest("Integration business",slug??"business-"+Guid.NewGuid().ToString("N"),type,custom))};
        request.Headers.Add("Idempotency-Key",(key??Guid.NewGuid()).ToString());return await Read<TenantView>(await client.SendAsync(request),HttpStatusCode.Created);
    }
    private async Task<(TenantView Tenant,HttpClient Owner,HttpClient Admin)> Active()
    {
        var user=await fixture.User();using var onboarding=Factory.Client(user.Tokens.Access);var tenant=await Register(onboarding);
        tenant=await Read<TenantView>(await onboarding.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/submit",new VersionRequest(tenant.Version)));
        var admin=Factory.Client((await fixture.Admin()).Access);tenant=await Read<TenantView>(await admin.PostAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/activate",new VersionRequest(tenant.Version)));
        var selected=await Protocol.SignIn(fixture.Identity.Factory,user.Email,user.Password,tenant.Id,scope:TenantFixture.Scopes);
        return(tenant,Factory.Client(selected.Access),admin);
    }
    private static SettingsRequest Settings(string version,string name="Updated business")=>new(version,"INR","en-IN","Asia/Kolkata",name,"https://example.test/logo.png");
    [Fact]
    public async Task Real_identity_pkce_onboarding_activation_settings_and_entitlements_work()
    {
        var (tenant,owner,admin)=await Active();using(owner)using(admin)
        {
            Assert.Equal(TenantStatus.Active,tenant.Status);
            var result=await Read<TenantView>(await owner.PutAsJsonAsync($"/api/v1/tenants/{tenant.Id}/settings",Settings(tenant.Version)));
            Assert.NotEqual(tenant.Version,result.Version);
            Assert.Equal("Updated business",(await owner.GetFromJsonAsync<SettingsView>($"/api/v1/tenants/{tenant.Id}/settings"))!.DisplayName);
            var entitlements=await owner.GetFromJsonAsync<EntitlementView>($"/api/v1/tenants/{tenant.Id}/entitlements");Assert.True(entitlements!.Active);Assert.Contains(entitlements.Modules,m=>m.Module=="catalog" && m.Enabled);
        }
    }
    [Fact]
    public async Task Registration_key_is_idempotent_and_payload_reuse_conflicts()
    {
        var user=await fixture.User();using var client=Factory.Client(user.Tokens.Access);var key=Guid.NewGuid();var slug="retry-"+Guid.NewGuid().ToString("N");
        var first=await Register(client,key,slug);var second=await Register(client,key,slug);Assert.Equal(first.Id,second.Id);
        using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/tenants"){Content=JsonContent.Create(new RegistrationRequest("Changed",slug,"spices",null))};request.Headers.Add("Idempotency-Key",key.ToString());
        Assert.Equal(HttpStatusCode.Conflict,(await client.SendAsync(request)).StatusCode);
    }
    [Fact]
    public async Task Unknown_type_missing_custom_type_and_customer_registration_are_rejected()
    {
        var user=await fixture.User();using var owner=Factory.Client(user.Tokens.Access);
        foreach(var type in new[]{"unknown","custom"})
        {
            using var request=new HttpRequestMessage(HttpMethod.Post,"/api/v1/tenants"){Content=JsonContent.Create(new RegistrationRequest("Valid name","type-"+Guid.NewGuid().ToString("N"),type,null))};request.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());Assert.Equal(HttpStatusCode.BadRequest,(await owner.SendAsync(request)).StatusCode);
        }
        Assert.NotNull(await Register(owner,type:"custom",custom:"Handmade instruments"));
        var customer=await fixture.User(AccountKind.Customer);using var client=Factory.Client(customer.Tokens.Access);
        using var invalid=new HttpRequestMessage(HttpMethod.Post,"/api/v1/tenants"){Content=JsonContent.Create(new RegistrationRequest("Customer business","customer-"+Guid.NewGuid().ToString("N"),"grocery",null))};invalid.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());Assert.Equal(HttpStatusCode.Forbidden,(await client.SendAsync(invalid)).StatusCode);
    }
    [Fact]
    public async Task Cross_tenant_and_header_spoofing_cannot_read_or_write_data()
    {
        var a=await Active();var b=await Active();using(a.Owner)using(a.Admin)using(b.Owner)using(b.Admin)
        {
            a.Owner.DefaultRequestHeaders.Add("X-Tenant-Id",b.Tenant.Id.ToString());
            Assert.Equal(HttpStatusCode.NotFound,(await a.Owner.GetAsync($"/api/v1/tenants/{b.Tenant.Id}/settings")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound,(await a.Owner.PutAsJsonAsync($"/api/v1/tenants/{b.Tenant.Id}/settings",Settings(b.Tenant.Version))).StatusCode);
            Assert.Equal("Integration business",(await b.Owner.GetFromJsonAsync<SettingsView>($"/api/v1/tenants/{b.Tenant.Id}/settings"))!.DisplayName);
        }
    }
    [Fact]
    public async Task Identity_only_token_and_revoked_session_are_denied()
    {
        var user=await fixture.User();var identityOnly=await Protocol.SignIn(fixture.Identity.Factory,user.Email,user.Password);using var client=Factory.Client(identityOnly.Access);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/plans")).StatusCode);
        using var full=Factory.Client(user.Tokens.Access);Assert.Equal(HttpStatusCode.OK,(await full.GetAsync("/api/v1/plans")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await user.Tokens.Client.PostAsync("/api/v1/logout",null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await full.GetAsync("/api/v1/plans")).StatusCode);
    }
    [Fact]
    public async Task Identity_outage_fails_closed_without_a_cached_role()
    {
        var user=await fixture.User();using var client=Factory.Client(user.Tokens.Access);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/api/v1/plans")).StatusCode);
        try{Factory.IdentityOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await client.GetAsync("/api/v1/plans")).StatusCode);}finally{Factory.IdentityOffline=false;}
    }
    [Fact]
    public async Task Failed_owner_provisioning_does_not_activate_the_tenant()
    {
        var user=await fixture.User();using var owner=Factory.Client(user.Tokens.Access);var tenant=await Register(owner);tenant=await Read<TenantView>(await owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/submit",new VersionRequest(tenant.Version)));
        using var admin=Factory.Client((await fixture.Admin()).Access);
        try
        {
            Factory.ProvisioningUnavailable=true;
            var response=await admin.PostAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/activate",new VersionRequest(tenant.Version));
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
        }
        finally{Factory.ProvisioningUnavailable=false;}
        Assert.Equal(TenantStatus.PendingApproval,(await owner.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{tenant.Id}"))!.Status);
    }
    [Fact]
    public async Task Stale_versions_and_concurrent_changes_do_not_lose_updates()
    {
        var item=await Active();using(item.Owner)using(item.Admin)
        {
            var responses=await Task.WhenAll(item.Owner.PutAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/settings",Settings(item.Tenant.Version,"Concurrent A")),item.Owner.PutAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/settings",Settings(item.Tenant.Version,"Concurrent B")));
            Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
            Assert.Equal(HttpStatusCode.Conflict,(await item.Owner.PutAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/settings",Settings(item.Tenant.Version))).StatusCode);
        }
    }
    [Fact]
    public async Task Location_quota_and_plan_downgrade_are_enforced_without_deleting_data()
    {
        var item=await Active();using(item.Owner)using(item.Admin)
        {
            var tenant=await Read<TenantView>(await item.Owner.PostAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/locations",new LocationRequest(item.Tenant.Version,"Main","Verified address","IN")));
            Assert.Equal(HttpStatusCode.Conflict,(await item.Owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/locations",new LocationRequest(tenant.Version,"Second","Another address","IN"))).StatusCode);
            tenant=await Read<TenantView>(await item.Admin.PutAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/subscription",new SubscriptionRequest(tenant.Version,"growth",DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(30),SubscriptionStatus.Active)));
            tenant=await Read<TenantView>(await item.Owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/locations",new LocationRequest(tenant.Version,"Second","Another address","IN")));
            Assert.Equal(HttpStatusCode.Conflict,(await item.Admin.PutAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/subscription",new SubscriptionRequest(tenant.Version,"starter",DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(30),SubscriptionStatus.Active))).StatusCode);
            Assert.Equal(2,(await item.Owner.GetFromJsonAsync<LocationView[]>($"/api/v1/tenants/{tenant.Id}/locations"))!.Count(l=>l.IsActive));
        }
    }
    [Fact]
    public async Task Subscription_expiration_and_suspension_disable_effective_entitlements()
    {
        var item=await Active();using(item.Owner)using(item.Admin)
        {
            var tenant=await Read<TenantView>(await item.Admin.PostAsJsonAsync($"/api/v1/platform/tenants/{item.Tenant.Id}/suspend",new SuspensionRequest(item.Tenant.Version,"Development lifecycle test")));
            Assert.False((await item.Owner.GetFromJsonAsync<EntitlementView>($"/api/v1/tenants/{tenant.Id}/entitlements"))!.Active);
            tenant=await Read<TenantView>(await item.Admin.PostAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/activate",new VersionRequest(tenant.Version)));
            tenant=await Read<TenantView>(await item.Admin.PutAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/subscription",new SubscriptionRequest(tenant.Version,"starter",DateTimeOffset.UtcNow.AddDays(-2),DateTimeOffset.UtcNow.AddDays(-1),SubscriptionStatus.Active)));
            Assert.False((await item.Owner.GetFromJsonAsync<EntitlementView>($"/api/v1/tenants/{tenant.Id}/entitlements"))!.Active);
        }
    }
    [Fact]
    public async Task Unentitled_modules_and_unsafe_branding_are_rejected()
    {
        var item=await Active();using(item.Owner)using(item.Admin)
        {
            Assert.Equal(HttpStatusCode.Conflict,(await item.Owner.PutAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/modules/manufacturing",new ModuleRequest(item.Tenant.Version,true))).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest,(await item.Owner.PutAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/settings",Settings(item.Tenant.Version) with{LogoUrl="javascript:alert(1)"})).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden,(await item.Owner.PostAsJsonAsync("/api/v1/platform/plans",new PlanRequest("owner-plan","Forbidden plan",1,1,5,["catalog"]))).StatusCode);
        }
    }
    [Fact]
    public async Task Domain_requires_correct_txt_challenge_and_respects_uniqueness_and_quota()
    {
        var item=await Active();using(item.Owner)using(item.Admin)
        {
            var host="shop-"+Guid.NewGuid().ToString("N")+".example.test";
            var challenge=await Read<DomainChallenge>(await item.Owner.PostAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/domains",new DomainRequest(item.Tenant.Version,host)));
            Assert.Equal(HttpStatusCode.Conflict,(await item.Owner.PostAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/domains/{challenge.Id}/verify",new DomainVerificationRequest(challenge.Version,challenge.Token,true))).StatusCode);
            Factory.Dns.Records[challenge.RecordName]=["srecommerce-verification="+challenge.Token];
            var tenant=await Read<TenantView>(await item.Owner.PostAsJsonAsync($"/api/v1/tenants/{item.Tenant.Id}/domains/{challenge.Id}/verify",new DomainVerificationRequest(challenge.Version,challenge.Token,true)));
            var domains=Assert.IsType<DomainView[]>(await item.Owner.GetFromJsonAsync<DomainView[]>($"/api/v1/tenants/{tenant.Id}/domains"));Assert.True(domains.Single().IsPrimary);Assert.NotNull(domains.Single().VerifiedAt);
            Assert.Equal(HttpStatusCode.Conflict,(await item.Owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/domains",new DomainRequest(tenant.Version,"another-"+Guid.NewGuid().ToString("N")+".example.test"))).StatusCode);
            Assert.DoesNotContain(challenge.Token,await (await item.Owner.GetAsync($"/api/v1/tenants/{tenant.Id}/domains")).Content.ReadAsStringAsync());
        }
    }
    [Fact]
    public async Task Audits_and_outbox_are_atomic_and_failed_validation_leaves_neither()
    {
        var user=await fixture.User();using var client=Factory.Client(user.Tokens.Access);var tenant=await Register(client);
        using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        var before=await db.Outbox.IgnoreQueryFilters().CountAsync(e=>e.TenantId==tenant.Id);
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PutAsJsonAsync($"/api/v1/tenants/{tenant.Id}/settings",Settings(tenant.Version) with{Currency="123"})).StatusCode);
        Assert.Equal(before,await db.Outbox.IgnoreQueryFilters().CountAsync(e=>e.TenantId==tenant.Id));
        Assert.Equal(before,await db.Audit.IgnoreQueryFilters().CountAsync(e=>e.TenantId==tenant.Id));
        Assert.Equal(0,await db.Settings.CountAsync(s=>s.TenantId==tenant.Id)); // No authorized scope in this independent service scope.
    }
    [Fact]
    public async Task Migration_reapply_and_health_contracts_work()
    {
        using var client=Factory.Client();Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/v1/tenants")).StatusCode);
        using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<TenantDbContext>();Assert.Empty(await db.Database.GetPendingMigrationsAsync());await db.Database.MigrateAsync();
    }
    [Fact]
    public async Task Platform_plan_creation_is_audited_and_profile_kind_cannot_grant_platform_role()
    {
        var pretender=await fixture.User(AccountKind.SuperAdmin);using var denied=Factory.Client(pretender.Tokens.Access);var code="plan-"+Guid.NewGuid().ToString("N");var request=new PlanRequest(code,"Versioned test plan",2,2,20,["catalog","storefront"]);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.PostAsJsonAsync("/api/v1/platform/plans",request)).StatusCode);
        using var admin=Factory.Client((await fixture.Admin()).Access);Assert.Equal(HttpStatusCode.NoContent,(await admin.PostAsJsonAsync("/api/v1/platform/plans",request)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync("/api/v1/platform/plans",request)).StatusCode);
        var history=await admin.GetFromJsonAsync<JsonElement[]>("/api/v1/platform/audit");Assert.Contains(history!,e=>e.GetProperty("action").GetString()=="plan.created" && e.GetProperty("resource").GetString()==code);
        Assert.Equal(HttpStatusCode.Forbidden,(await denied.GetAsync("/api/v1/platform/audit")).StatusCode);
    }
    [Fact]
    public async Task Membership_demotion_revokes_old_owner_access_and_new_staff_cannot_write()
    {
        var user=await fixture.User();using var onboarding=Factory.Client(user.Tokens.Access);var tenant=await Register(onboarding);tenant=await Read<TenantView>(await onboarding.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/submit",new VersionRequest(tenant.Version)));
        var administrator=await fixture.Admin();using var admin=Factory.Client(administrator.Access);tenant=await Read<TenantView>(await admin.PostAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/activate",new VersionRequest(tenant.Version)));
        var ownerToken=await Protocol.SignIn(fixture.Identity.Factory,user.Email,user.Password,tenant.Id,scope:TenantFixture.Scopes);using var owner=Factory.Client(ownerToken.Access);
        var replacement=await fixture.User();Assert.Equal(HttpStatusCode.NoContent,(await administrator.Client.PutAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/memberships/{replacement.Id}",new{userId=replacement.Id,role="Owner"})).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,(await administrator.Client.PutAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/memberships/{user.Id}",new{userId=user.Id,role="Staff"})).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await owner.GetAsync($"/api/v1/tenants/{tenant.Id}/settings")).StatusCode);
        var staffToken=await Protocol.SignIn(fixture.Identity.Factory,user.Email,user.Password,tenant.Id,scope:TenantFixture.Scopes);using var staff=Factory.Client(staffToken.Access);
        Assert.Equal(HttpStatusCode.OK,(await staff.GetAsync($"/api/v1/tenants/{tenant.Id}/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,(await staff.PutAsJsonAsync($"/api/v1/tenants/{tenant.Id}/settings",Settings(tenant.Version))).StatusCode);
    }
    [Fact]
    public async Task Archive_requires_suspension_and_preserves_readable_records()
    {
        var item=await Active();using(item.Owner)using(item.Admin)
        {
            Assert.Equal(HttpStatusCode.Conflict,(await item.Admin.PostAsJsonAsync($"/api/v1/platform/tenants/{item.Tenant.Id}/archive",new VersionRequest(item.Tenant.Version))).StatusCode);
            var tenant=await Read<TenantView>(await item.Admin.PostAsJsonAsync($"/api/v1/platform/tenants/{item.Tenant.Id}/suspend",new SuspensionRequest(item.Tenant.Version,"Archive test")));
            tenant=await Read<TenantView>(await item.Admin.PostAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/archive",new VersionRequest(tenant.Version)));Assert.Equal(TenantStatus.Archived,tenant.Status);
            Assert.Equal(HttpStatusCode.Conflict,(await item.Owner.PutAsJsonAsync($"/api/v1/tenants/{tenant.Id}/settings",Settings(tenant.Version))).StatusCode);
            Assert.Equal(HttpStatusCode.OK,(await item.Admin.GetAsync($"/api/v1/tenants/{tenant.Id}/settings")).StatusCode);
        }
    }
    [Fact]
    public async Task Unscoped_persistence_cannot_modify_a_tenant_root()
    {
        var user=await fixture.User();using var owner=Factory.Client(user.Tokens.Access);var tenant=await Register(owner);
        using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<TenantDbContext>();var root=await db.Tenants.IgnoreQueryFilters().SingleAsync(t=>t.Id==tenant.Id);root.Name="Unauthorized mutation";
        await Assert.ThrowsAsync<TenantRuleException>(()=>db.SaveChangesAsync());
        Assert.Equal("Integration business",(await owner.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{tenant.Id}"))!.Name);
    }
    [Fact]
    public async Task Domain_cannot_be_reserved_by_another_tenant_or_verified_after_expiry()
    {
        var first=await fixture.User();var second=await fixture.User();using var a=Factory.Client(first.Tokens.Access);using var b=Factory.Client(second.Tokens.Access);var ta=await Register(a);var tb=await Register(b);var host="reserved-"+Guid.NewGuid().ToString("N")+".example.test";
        var challenge=await Read<DomainChallenge>(await a.PostAsJsonAsync($"/api/v1/tenants/{ta.Id}/domains",new DomainRequest(ta.Version,host)));
        Assert.Equal(HttpStatusCode.Conflict,(await b.PostAsJsonAsync($"/api/v1/tenants/{tb.Id}/domains",new DomainRequest(tb.Version,host))).StatusCode);
        using(var scope=Factory.Services.CreateScope())
        {
            var db=scope.ServiceProvider.GetRequiredService<TenantDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.TenantDomains SET VerificationExpiresAt={DateTimeOffset.UtcNow.AddMinutes(-1)} WHERE Id={challenge.Id}");
        }
        Factory.Dns.Records[challenge.RecordName]=["srecommerce-verification="+challenge.Token];
        Assert.Equal(HttpStatusCode.BadRequest,(await a.PostAsJsonAsync($"/api/v1/tenants/{ta.Id}/domains/{challenge.Id}/verify",new DomainVerificationRequest(challenge.Version,challenge.Token))).StatusCode);
    }
    [Fact]
    public async Task Owner_deactivated_after_submission_cannot_activate_a_business()
    {
        var user=await fixture.User();using var owner=Factory.Client(user.Tokens.Access);var tenant=await Register(owner);tenant=await Read<TenantView>(await owner.PostAsJsonAsync($"/api/v1/tenants/{tenant.Id}/submit",new VersionRequest(tenant.Version)));
        var token=await fixture.Admin();using var admin=Factory.Client(token.Access);
        Assert.Equal(HttpStatusCode.NoContent,(await token.Client.PutAsJsonAsync($"/api/v1/platform/accounts/{user.Id}/activation",new{isActive=false})).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await admin.PostAsJsonAsync($"/api/v1/platform/tenants/{tenant.Id}/activate",new VersionRequest(tenant.Version))).StatusCode);
        Assert.Equal(TenantStatus.PendingApproval,(await admin.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{tenant.Id}"))!.Status);
    }
}
