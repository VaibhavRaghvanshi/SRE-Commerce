using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Infrastructure;
using SRECommerce.Identity.Tests;
using SRECommerce.Tenant.API;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Infrastructure;
using Xunit;

namespace SRECommerce.Tenant.Tests;

public sealed class TestDns : IDomainOwnershipVerifier
{
    public Dictionary<string,string[]> Records { get; } = new();
    public Task<IReadOnlyCollection<string>> GetTxtAsync(string host,CancellationToken ct)=>Task.FromResult<IReadOnlyCollection<string>>(Records.GetValueOrDefault(host)??[]);
}
public sealed class IdentityTestHandler(HttpMessageHandler inner,TenantFactory factory) : DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {
        if(factory.IdentityOffline) throw new HttpRequestException("Simulated Identity outage");
        if(factory.ProvisioningUnavailable && request.Method==HttpMethod.Put) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        return base.SendAsync(request,ct);
    }
}
public sealed class TenantFactory(IdentityFixture identity) : WebApplicationFactory<TenantProgram>
{
    public string DatabaseName { get; }="SRECommerce_TenantTests_"+Guid.NewGuid().ToString("N");
    public string Connection => $"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
    public TestDns Dns { get; }=new();
    public bool IdentityOffline { get; set; }
    public bool ProvisioningUnavailable { get; set; }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");builder.UseSetting("ConnectionStrings:Tenant",Connection);builder.UseSetting("Identity:BaseUrl","https://localhost/");
        builder.ConfigureServices(services=>
        {
            services.RemoveAll<IDomainOwnershipVerifier>();services.AddSingleton<IDomainOwnershipVerifier>(Dns);
            services.AddHttpClient<IdentityConnection>().ConfigurePrimaryHttpMessageHandler(()=>new IdentityTestHandler(identity.Factory.Server.CreateHandler(),this));
        });
        builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));
    }
    public HttpClient Client(string? token=null)
    {
        var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});
        if(token is not null)client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);return client;
    }
}
public sealed class TenantFixture : IDisposable
{
    public const string Scopes="openid profile email offline_access identity_api tenant_api";
    public IdentityFixture Identity { get; }=new();
    public TenantFactory Factory { get; }
    public TenantFixture()
    {
        Factory=new(Identity);using var client=Factory.Client();using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<TenantDbContext>();
        if(db.Database.GetDbConnection().Database!=Factory.DatabaseName || !Factory.DatabaseName.StartsWith("SRECommerce_TenantTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong Tenant test database");
        db.Database.Migrate();TenantSetup.Initialize(db).GetAwaiter().GetResult();
        Console.WriteLine("Isolated Tenant integration database: "+Factory.DatabaseName);
    }
    public async Task<(Guid Id,string Email,string Password,TokenSet Tokens)> User(AccountKind kind=AccountKind.BusinessOwner)
    {
        var email=Guid.NewGuid().ToString("N")+"@example.test";var password=Protocol.Password();
        using var scope=Identity.Factory.Services.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();var user=new ApplicationUser{Id=Guid.NewGuid(),UserName=email,Email=email,EmailConfirmed=true,LockoutEnabled=true};
        Assert.True((await manager.CreateAsync(user,password)).Succeeded);var db=scope.ServiceProvider.GetRequiredService<IdentityDbContext>();db.Profiles.Add(new UserProfile{UserId=user.Id,FullName="Tenant integration owner",Kind=kind});await db.SaveChangesAsync();
        return(user.Id,email,password,await Protocol.SignIn(Identity.Factory,email,password,scope:Scopes));
    }
    public Task<TokenSet> Admin()=>Protocol.SignIn(Identity.Factory,Identity.Factory.AdminEmail,Identity.Factory.Password,scope:Scopes);
    public void Dispose(){Factory.Dispose();Identity.Dispose();} // Retain isolated DBs for inspection; never drop data implicitly.
}
