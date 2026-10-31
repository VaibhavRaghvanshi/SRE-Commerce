using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Identity.Tests;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;
using SRECommerce.Tenant.Tests;
using SRECommerce.Catalog.API;
using SRECommerce.Catalog.Infrastructure;
using Xunit;

namespace SRECommerce.Catalog.Tests;

public sealed class DependencyTestHandler(HttpMessageHandler inner,Func<bool> offline):DelegatingHandler(inner)
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
    {if(offline())throw new HttpRequestException("Simulated dependency outage");return base.SendAsync(request,ct);}
}
public sealed class CatalogFactory(TenantFixture tenant):WebApplicationFactory<CatalogProgram>
{
    public string DatabaseName{get;}="SRECommerce_CatalogTests_"+Guid.NewGuid().ToString("N");
    public bool SqlOffline{get;init;}
    public string Connection=>$"Server=localhost,{(SqlOffline?1:1433)};Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true;Connect Timeout={(SqlOffline?1:15)}";
    public bool IdentityOffline{get;set;}public bool TenantOffline{get;set;}
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");builder.UseSetting("ConnectionStrings:Catalog",Connection);
        builder.UseSetting("Identity:BaseUrl","https://localhost/");builder.UseSetting("Tenant:BaseUrl","https://localhost/");builder.UseSetting("RateLimiting:PermitLimit","1000");
        builder.ConfigureServices(services=>
        {
            services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(tenant.Identity.Factory.Server.CreateHandler(),()=>IdentityOffline));
            services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(tenant.Factory.Server.CreateHandler(),()=>TenantOffline));
        });
        builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));
    }
    public HttpClient Client(string? token=null)
    {var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});if(token is not null)client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);return client;}
}
public record CatalogBusiness(Guid Id,HttpClient Client,string Token,string Email,string Password);
public sealed class CatalogFixture:IDisposable
{
    public const string Scopes="openid profile email offline_access identity_api tenant_api commerce_api";
    public TenantFixture Tenant{get;}=new();public CatalogFactory Factory{get;}
    public CatalogFixture()
    {
        Factory=new(Tenant);using var client=Factory.Client();using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        if(db.Database.GetDbConnection().Database!=Factory.DatabaseName||!Factory.DatabaseName.StartsWith("SRECommerce_CatalogTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong Catalog test database");
        db.Database.Migrate();
        Console.WriteLine("Isolated Catalog integration database: "+Factory.DatabaseName);
    }
    public static async Task<T> Read<T>(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK)
    {Assert.True(response.StatusCode==expected,$"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>())!;}
    public async Task<CatalogBusiness> Business()
    {
        var user=await Tenant.User();using var onboarding=Tenant.Factory.Client(user.Tokens.Access);
        using var registration=new HttpRequestMessage(HttpMethod.Post,"/api/v1/tenants"){Content=JsonContent.Create(new SRECommerce.Tenant.Application.RegistrationRequest("Catalog SQL tests","catalog-"+Guid.NewGuid().ToString("N"),"spices",null))};registration.Headers.Add("Idempotency-Key",Guid.NewGuid().ToString());
        var value=await Read<TenantView>(await onboarding.SendAsync(registration),HttpStatusCode.Created);
        value=await Read<TenantView>(await onboarding.PostAsJsonAsync($"/api/v1/tenants/{value.Id}/submit",new VersionRequest(value.Version)));
        using var admin=Tenant.Factory.Client((await Tenant.Admin()).Access);
        value=await Read<TenantView>(await admin.PostAsJsonAsync($"/api/v1/platform/tenants/{value.Id}/activate",new VersionRequest(value.Version)));
        value=await Read<TenantView>(await admin.PutAsJsonAsync($"/api/v1/platform/tenants/{value.Id}/subscription",new SubscriptionRequest(value.Version,"growth",DateTimeOffset.UtcNow.AddMinutes(-1),DateTimeOffset.UtcNow.AddDays(30),SubscriptionStatus.Active)));
        var tokens=await Protocol.SignIn(Tenant.Identity.Factory,user.Email,user.Password,value.Id,scope:Scopes);
        using var owner=Tenant.Factory.Client(tokens.Access);
        await Read<TenantView>(await owner.PutAsJsonAsync($"/api/v1/tenants/{value.Id}/modules/catalog",new ModuleRequest(value.Version,true)));
        return new(value.Id,Factory.Client(tokens.Access),tokens.Access,user.Email,user.Password);
    }
    public void Dispose(){Factory.Dispose();Tenant.Dispose();} // Retain isolated SQL databases for inspection.
}
