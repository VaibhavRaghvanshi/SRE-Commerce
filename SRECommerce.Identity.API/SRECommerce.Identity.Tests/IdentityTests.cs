using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using SRECommerce.Identity.API;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Infrastructure;
using Xunit;

namespace SRECommerce.Identity.Tests;

public sealed class IdentityTests(IdentityFixture fixture) : IClassFixture<IdentityFixture>
{
    private IdentityFactory Factory => fixture.Factory;
    private async Task<(Guid Id, string Email, string Password)> NewUser(bool confirm = true, AccountKind kind = AccountKind.Customer)
    {
        var email = Guid.NewGuid().ToString("N") + "@example.test";
        var password = Protocol.Password();
        using var client = Factory.Client();
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/accounts/register", new { email, password, fullName = "SQL Integration User", kind })).StatusCode);
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        if (confirm)
        {
            var code = await EmailCode(user.Id, "confirm-email");
            Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/accounts/confirm-email", new { email, code })).StatusCode);
        }
        return (user.Id, email, password);
    }
    private async Task<string> EmailCode(Guid userId, string purpose)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var jobs = await db.Emails.Where(e => e.UserId == userId).OrderByDescending(e => e.CreatedAt).ToListAsync();
        var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("SRECommerce.Identity.Email.v1");
        return jobs.Select(e => JsonSerializer.Deserialize<JsonElement>(protector.Unprotect(e.Body))).First(e => e.GetProperty("purpose").GetString() == purpose).GetProperty("code").GetString()!;
    }
    private async Task<Guid> Provision(Guid userId, string role, Guid? tenant = null)
    {
        var tenantId = tenant ?? Guid.NewGuid();
        var admin = await Protocol.SignIn(Factory, Factory.AdminEmail, Factory.Password);
        using var client = admin.Client;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync($"/api/v1/platform/tenants/{tenantId}/memberships/{userId}", new { userId, role })).StatusCode);
        return tenantId;
    }

    [Fact]
    public async Task Registration_verification_pkce_profile_and_health_work_on_sql_server()
    {
        var user = await NewUser();
        var tokens = await Protocol.SignIn(Factory, user.Email, user.Password);
        using var client = tokens.Client;
        var me = await client.GetFromJsonAsync<JsonElement>("/api/v1/me");
        Assert.Equal(user.Id, me.GetProperty("id").GetGuid());
        Assert.True(me.GetProperty("emailConfirmed").GetBoolean());
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/api/v1/me", new { fullName = "Updated Profile" })).StatusCode);
        Assert.Equal("Updated Profile", (await client.GetFromJsonAsync<JsonElement>("/api/v1/me")).GetProperty("fullName").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health/ready")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/.well-known/openid-configuration")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        await db.Database.MigrateAsync(); // Reapply is idempotent, not EnsureCreated.
    }

    [Fact]
    public async Task Unconfirmed_login_is_denied_and_registration_cannot_self_assign_admin()
    {
        var user = await NewUser(confirm:false);
        using var client = Factory.Client();
        var html = await client.GetStringAsync("/account/login");
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string,string> { ["email"] = user.Email, ["password"] = user.Password, ["returnUrl"] = "/connect/authorize", ["__RequestVerificationToken"] = Protocol.Csrf(html) }));
        Assert.Contains("error=true", response.Headers.Location!.OriginalString);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/accounts/register", new { email = "elevate@example.test", password = Protocol.Password(), fullName = "Untrusted", kind = 3 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/accounts/register", new { email = "invalid", password = "short", fullName = "" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/accounts/register", new { email = Guid.NewGuid()+"@example.test", password = Protocol.Password(), fullName = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/accounts/forgot-password", new { email = "absent@example.test" })).StatusCode);
    }

    [Fact]
    public async Task Password_reset_revokes_old_session_and_invalid_reset_is_rejected()
    {
        var user = await NewUser();
        var tokens = await Protocol.SignIn(Factory, user.Email, user.Password);
        using var client = tokens.Client;
        Assert.Equal(HttpStatusCode.Accepted, (await client.PostAsJsonAsync("/api/v1/accounts/forgot-password", new { email = user.Email })).StatusCode);
        var password = Protocol.Password();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/accounts/reset-password", new { email = user.Email, code = "invalid", password })).StatusCode);
        var code = await EmailCode(user.Id, "reset-password");
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/api/v1/accounts/reset-password", new { email = user.Email, code, password })).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/accounts/reset-password", new { email = user.Email, code, password = Protocol.Password() })).StatusCode);
        var next = await Protocol.SignIn(Factory, user.Email, password);
        next.Client.Dispose();
    }

    [Fact]
    public async Task Refresh_rotates_and_replay_revokes_authorization()
    {
        var user = await NewUser();
        var tokens = await Protocol.SignIn(Factory, user.Email, user.Password);
        using var client = tokens.Client;
        var form = new Dictionary<string,string> { ["grant_type"] = "refresh_token", ["client_id"] = "srecommerce-local", ["refresh_token"] = tokens.Refresh };
        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var next = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(tokens.Refresh, next.GetProperty("refresh_token").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/connect/token", new FormUrlEncodedContent(form))).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", next.GetProperty("access_token").GetString());
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_access_and_refresh()
    {
        var user = await NewUser();
        var tokens = await Protocol.SignIn(Factory, user.Email, user.Password);
        using var client = tokens.Client;
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/v1/logout", null)).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string,string> { ["grant_type"] = "refresh_token", ["client_id"] = "srecommerce-local", ["refresh_token"] = tokens.Refresh }))).StatusCode);
    }

    [Fact]
    public async Task Missing_pkce_unknown_redirect_and_password_grant_are_rejected()
    {
        using var client = Factory.Client();
        var noPkce = await client.GetAsync("/connect/authorize?client_id=srecommerce-local&response_type=code&redirect_uri=http%3A%2F%2F127.0.0.1%3A5099%2Fcallback&scope=openid");
        Assert.True(noPkce.StatusCode == HttpStatusCode.BadRequest || (noPkce.StatusCode == HttpStatusCode.Redirect && noPkce.Headers.Location!.Query.Contains("error=")));
        var redirect = await client.GetAsync("/connect/authorize?client_id=srecommerce-local&response_type=code&redirect_uri=https%3A%2F%2Funtrusted.example%2Fcallback&scope=openid");
        Assert.Equal(HttpStatusCode.BadRequest, redirect.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string,string>{["grant_type"]="password",["client_id"]="srecommerce-local"}))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string,string>{["email"]="x@example.test",["password"]="not-a-credential"}))).StatusCode);
    }

    [Fact]
    public async Task Tenant_filters_permissions_and_privileged_audit_deny_cross_tenant_access()
    {
        var owner = await NewUser();
        var tenantA = await Provision(owner.Id, "Owner");
        var staff = await NewUser();
        await Provision(staff.Id, "Staff", tenantA);
        var other = await NewUser();
        var tenantB = await Provision(other.Id, "Owner");
        var token = await Protocol.SignIn(Factory, owner.Email, owner.Password, tenantA);
        using var client = token.Client;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/v1/tenants/{tenantA}/memberships")).StatusCode);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantB.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/v1/tenants/{tenantB}/memberships")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/v1/tenants/{tenantA}/memberships/{staff.Id}", new { userId=staff.Id, role="Owner" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PutAsJsonAsync($"/api/v1/platform/accounts/{other.Id}/activation", new { isActive=false })).StatusCode);
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        Assert.Empty(await db.Memberships.ToListAsync()); // No signed tenant context in this scope.
        Assert.True(await db.AuditLogs.AnyAsync(a => a.TenantId == tenantA && a.Action == "platform.membership.provision"));
    }

    [Fact]
    public async Task Account_deactivation_invalidates_existing_tokens()
    {
        var user = await NewUser();
        var tokens = await Protocol.SignIn(Factory, user.Email, user.Password);
        using var client = tokens.Client;
        var admin = await Protocol.SignIn(Factory, Factory.AdminEmail, Factory.Password);
        using var adminClient = admin.Client;
        Assert.Equal(HttpStatusCode.NoContent, (await adminClient.PutAsJsonAsync($"/api/v1/platform/accounts/{user.Id}/activation", new { isActive=false })).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Mfa_enrollment_requires_password_and_code_then_requires_mfa_login()
    {
        var user = await NewUser();
        var tokens = await Protocol.SignIn(Factory, user.Email, user.Password);
        using var client = tokens.Client;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/me/mfa/setup", new { password="wrong" })).StatusCode);
        var setup = await client.PostAsJsonAsync("/api/v1/me/mfa/setup", new { password=user.Password });
        Assert.Equal(HttpStatusCode.OK, setup.StatusCode);
        var key = (await setup.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sharedKey").GetString()!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/v1/me/mfa/enable", new { code="invalid" })).StatusCode);
        var enable = await client.PostAsJsonAsync("/api/v1/me/mfa/enable", new { code=Protocol.Totp(key) });
        Assert.Equal(HttpStatusCode.OK, enable.StatusCode);
        var recoveryCode=(await enable.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("recoveryCodes")[0].GetString()!;
        Assert.NotEqual(HttpStatusCode.OK, (await client.GetAsync("/api/v1/me")).StatusCode);
        var mfa = await Protocol.SignIn(Factory, user.Email, user.Password, mfaCode:Protocol.Totp(key));
        using var mfaClient = mfa.Client;
        Assert.Equal(HttpStatusCode.OK, (await mfaClient.GetAsync("/api/v1/me")).StatusCode);
        var recovery=await Protocol.SignIn(Factory,user.Email,user.Password,mfaCode:recoveryCode,recovery:true);
        recovery.Client.Dispose();
        using var scope=Factory.Services.CreateScope();
        var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(7,await manager.CountRecoveryCodesAsync((await manager.FindByIdAsync(user.Id.ToString()))!));
    }

    [Fact]
    public async Task Lockout_stops_repeated_bad_passwords_and_is_audited()
    {
        var user = await NewUser();
        using var client = Factory.Client();
        for (var i=0;i<5;i++)
        {
            var page = await client.GetStringAsync("/account/login");
            await client.PostAsync("/account/login", new FormUrlEncodedContent(new Dictionary<string,string>{["email"]=user.Email,["password"]="wrong",["__RequestVerificationToken"]=Protocol.Csrf(page)}));
        }
        using var scope = Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(user.Id.ToString()))!));
        Assert.True(await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().AuditLogs.AnyAsync(a=>a.UserId==user.Id && a.Action=="login" && a.Outcome=="failure"));
    }

    [Fact]
    public async Task Legacy_import_is_idempotent_unprivileged_and_rehashes_bcrypt()
    {
        using var scope = Factory.Services.CreateScope();
        var password = Protocol.Password();
        var email = "legacy-"+Guid.NewGuid().ToString("N")+"@example.test";
        var path = Path.Combine(Factory.KeyDirectory,"legacy-"+Guid.NewGuid().ToString("N")+".json");
        Directory.CreateDirectory(Factory.KeyDirectory);
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(new IdentitySetup.Snapshot("legacy-sqlite",[new(12345,"Legacy Admin",email,BCrypt.Net.BCrypt.HashPassword(password,4),"Admin")])));
        await IdentitySetup.ImportAsync(scope.ServiceProvider,path,false);
        await IdentitySetup.ImportAsync(scope.ServiceProvider,path,true);
        await IdentitySetup.ImportAsync(scope.ServiceProvider,path,true);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByEmailAsync(email))!;
        Assert.False(user.EmailConfirmed);
        Assert.Empty(await users.GetRolesAsync(user));
        Assert.NotNull(user.LegacyPasswordHash);
        Assert.True(await users.CheckPasswordAsync(user,password));
        Assert.Null(user.LegacyPasswordHash);
        Assert.NotEqual("LEGACY_BCRYPT",user.PasswordHash);
        Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().LegacyUsers.CountAsync(m=>m.UserId==user.Id));
    }

    [Fact]
    public async Task Membership_rowversion_prevents_lost_updates()
    {
        var user=await NewUser();
        var tenant=await Provision(user.Id,"Staff");
        using var a=Factory.Services.CreateScope(); using var b=Factory.Services.CreateScope();
        var dbA=a.ServiceProvider.GetRequiredService<IdentityDbContext>(); var dbB=b.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var first=await dbA.Memberships.IgnoreQueryFilters().SingleAsync(m=>m.TenantId==tenant && m.UserId==user.Id);
        var second=await dbB.Memberships.IgnoreQueryFilters().SingleAsync(m=>m.TenantId==tenant && m.UserId==user.Id);
        Assert.Equal(8,first.Version.Length);
        first.Role="Customer"; await dbA.SaveChangesAsync();
        second.Role="B2BBuyer"; await Assert.ThrowsAsync<DbUpdateConcurrencyException>(()=>dbB.SaveChangesAsync());
    }

    [Fact]
    public async Task Missing_mailpit_retains_encrypted_email_jobs()
    {
        var user=await NewUser(confirm:false);
        using var scope=Factory.Services.CreateScope();
        var job=await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Emails.SingleAsync(e=>e.UserId==user.Id);
        Assert.Null(job.DeliveredAt);
        Assert.DoesNotContain(user.Email,job.Body);
        Assert.False(string.IsNullOrWhiteSpace(await EmailCode(user.Id,"confirm-email")));
    }

    [Fact]
    public async Task Smtp_outage_preserves_jobs_with_retry_metadata()
    {
        await NewUser(confirm:false);
        using var scope=Factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var before=await db.Emails.CountAsync();
        var config=new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["Mailpit:Enabled"]="true",["Mailpit:Port"]="1"}).Build();
        using var worker=new LocalEmailWorker(Factory.Services.GetRequiredService<IServiceScopeFactory>(),config,Microsoft.Extensions.Logging.Abstractions.NullLogger<LocalEmailWorker>.Instance);
        await worker.StartAsync(CancellationToken.None);
        try
        {
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(12));
            while(!await db.Emails.AnyAsync(e=>e.Attempts>0,timeout.Token)) await Task.Delay(200,timeout.Token);
            Assert.Equal(before,await db.Emails.CountAsync());
            Assert.True(await db.Emails.AnyAsync(e=>e.Attempts>0 && e.DeliveredAt==null && e.NextAttemptAt>e.CreatedAt));
        }
        finally { await worker.StopAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task Other_user_cannot_revoke_sessions_and_tampered_tokens_are_denied()
    {
        var a=await NewUser(); var b=await NewUser();
        var tokenA=await Protocol.SignIn(Factory,a.Email,a.Password);
        using var aClient=tokenA.Client;
        var sessions=await aClient.GetFromJsonAsync<JsonElement>("/api/v1/me/sessions");
        var sessionId=sessions[0].GetProperty("id").GetGuid();
        var tokenB=await Protocol.SignIn(Factory,b.Email,b.Password);
        using var bClient=tokenB.Client;
        Assert.Equal(HttpStatusCode.NotFound,(await bClient.DeleteAsync($"/api/v1/me/sessions/{sessionId}")).StatusCode);
        var parts=tokenB.Access.Split('.');
        parts[1]=parts[1][0]=='a' ? "b"+parts[1][1..] : "a"+parts[1][1..];
        bClient.DefaultRequestHeaders.Authorization=new("Bearer",string.Join('.',parts));
        Assert.Equal(HttpStatusCode.Unauthorized,(await bClient.GetAsync("/api/v1/me")).StatusCode);
    }

    [Fact]
    public async Task Password_change_revokes_old_tokens_and_old_login_cookie()
    {
        var user=await NewUser();
        var token=await Protocol.SignIn(Factory,user.Email,user.Password);
        using var client=token.Client;
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/v1/me/password",new{currentPassword="wrong",newPassword=Protocol.Password()})).StatusCode);
        var next=Protocol.Password();
        Assert.Equal(HttpStatusCode.NoContent,(await client.PostAsJsonAsync("/api/v1/me/password",new{currentPassword=user.Password,newPassword=next})).StatusCode);
        Assert.NotEqual(HttpStatusCode.OK,(await client.GetAsync("/api/v1/me")).StatusCode);
        var fresh=await Protocol.SignIn(Factory,user.Email,next);
        fresh.Client.Dispose();
    }

    [Fact]
    public async Task Authentication_rate_limit_rejects_excess_login_requests()
    {
        using var factory=new IdentityFactory{PermitLimit=2};
        using var client=factory.Client();
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/account/login")).StatusCode);
        Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/account/login")).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests,(await client.GetAsync("/account/login")).StatusCode);
    }

    [Fact]
    public void Production_rejects_development_sql_certificate_bypass()
    {
        using var factory=new IdentityFactory{EnvironmentName="Production"};
        Assert.Throws<InvalidOperationException>(()=>factory.Client());
    }

    [Fact]
    public async Task Legacy_email_alias_requires_explicit_review_and_does_not_overwrite_credentials()
    {
        using var scope=Factory.Services.CreateScope();
        var email="alias-"+Guid.NewGuid().ToString("N")+"@example.test";
        var original=BCrypt.Net.BCrypt.HashPassword(Protocol.Password(),4);
        var path=Path.Combine(Factory.KeyDirectory,"alias-"+Guid.NewGuid().ToString("N")+".json");
        Directory.CreateDirectory(Factory.KeyDirectory);
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new IdentitySetup.Snapshot("legacy-sqlite",[new(67890,"Reviewed alias",email,original,"Admin")])));
        await IdentitySetup.ImportAsync(scope.ServiceProvider,path,true);
        await File.WriteAllTextAsync(path,JsonSerializer.Serialize(new IdentitySetup.Snapshot("legacy-sqlserver",[new(67890,"Old SQL alias",email,BCrypt.Net.BCrypt.HashPassword(Protocol.Password(),4),"Admin")])));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>IdentitySetup.ImportAsync(scope.ServiceProvider,path,false));
        await IdentitySetup.ImportAsync(scope.ServiceProvider,path,true,linkReviewedLegacyAliases:true);
        var user=(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync(email))!;
        Assert.Equal(original,user.LegacyPasswordHash);
        Assert.False(user.EmailConfirmed);
        Assert.Equal(2,await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().LegacyUsers.CountAsync(m=>m.UserId==user.Id));
    }

    [Fact]
    public async Task Concurrent_owner_demotions_cannot_remove_all_owners()
    {
        var first=await NewUser();var second=await NewUser();
        var tenant=await Provision(first.Id,"Owner");await Provision(second.Id,"Owner",tenant);
        var admin=await Protocol.SignIn(Factory,Factory.AdminEmail,Factory.Password);
        using var client=admin.Client;
        var responses=await Task.WhenAll(
            client.PutAsJsonAsync($"/api/v1/platform/tenants/{tenant}/memberships/{first.Id}",new{userId=first.Id,role="Staff"}),
            client.PutAsJsonAsync($"/api/v1/platform/tenants/{tenant}/memberships/{second.Id}",new{userId=second.Id,role="Staff"}));
        Assert.Contains(responses,r=>r.StatusCode==HttpStatusCode.NoContent);
        using var scope=Factory.Services.CreateScope();
        Assert.Equal(1,await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Memberships.IgnoreQueryFilters().CountAsync(m=>m.TenantId==tenant && m.Role=="Owner" && m.IsActive));
    }
}
