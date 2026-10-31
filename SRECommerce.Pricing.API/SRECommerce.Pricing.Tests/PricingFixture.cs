using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Domain;
using SRECommerce.Catalog.Tests;
using SRECommerce.Identity.Tests;
using SRECommerce.Pricing.API;
using SRECommerce.Pricing.Application;
using SRECommerce.Pricing.Domain;
using SRECommerce.Pricing.Infrastructure;
using SRECommerce.Tenant.Application;
using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;
namespace SRECommerce.Pricing.Tests;
public sealed class PricingClock:TimeProvider
{
    private long ticks=DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow()=>new(Interlocked.Read(ref ticks),TimeSpan.Zero);
    public void Advance(TimeSpan value)=>Interlocked.Add(ref ticks,value.Ticks);
}
public sealed class PricingFactory(CatalogFixture catalog):WebApplicationFactory<PricingProgram>
{
    public string DatabaseName{get;}="SRECommerce_PricingTests_"+Guid.NewGuid().ToString("N");
    public PricingClock Clock{get;}=new();public bool IdentityOffline{get;set;}public bool CatalogOffline{get;set;}public bool TenantOffline{get;set;}
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");builder.UseSetting("ConnectionStrings:Pricing",$"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");builder.UseSetting("RateLimiting:PermitLimit","1000");
        builder.ConfigureServices(services=>
        {
            services.AddSingleton<TimeProvider>(Clock);
            services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(catalog.Tenant.Identity.Factory.Server.CreateHandler(),()=>IdentityOffline));
            services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(catalog.Tenant.Factory.Server.CreateHandler(),()=>TenantOffline));
            services.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(catalog.Factory.Server.CreateHandler(),()=>CatalogOffline));
        });builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));
    }
    public HttpClient Client(string? token=null)
    {var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});if(token is not null)client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);return client;}
}
public record PricingBusiness(CatalogBusiness Catalog,HttpClient Client,Guid Product,Guid Variant,Guid Tax,Guid List);
public sealed class PricingFixture:IDisposable
{
    public CatalogFixture Catalog{get;}=new();public PricingFactory Factory{get;}
    private readonly Dictionary<string,string> machineSecrets=new();
    public PricingFixture()
    {
        Factory=new(Catalog);using var client=Factory.Client();using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PricingDbContext>();
        if(db.Database.GetDbConnection().Database!=Factory.DatabaseName||!Factory.DatabaseName.StartsWith("SRECommerce_PricingTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong Pricing test database");db.Database.Migrate();
    }
    public static string Path(Guid tenant)=>$"/api/v1/tenants/{tenant}/pricing";
    public static string Public(Guid tenant)=>$"/api/v1/public/tenants/{tenant}/pricing";
    public static string InternalPath(Guid tenant)=>$"/internal/v1/tenants/{tenant}/pricing";
    public static async Task<T> Read<T>(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK)
    {Assert.True(response.StatusCode==expected,$"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>())!;}
    public async Task<PricingBusiness> Business()
    {
        var b=await Catalog.Business();using var tenant=Catalog.Tenant.Factory.Client(b.Token);var value=(await tenant.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{b.Id}"))!;
        await Read<TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{b.Id}/modules/pricing",new ModuleRequest(value.Version,true)));
        var category=await Read<CatalogLookup>(await b.Client.PostAsJsonAsync($"/api/v1/tenants/{b.Id}/catalog/categories",new CategoryRequest("Price tests","price-tests",null)),HttpStatusCode.Created);
        var draft=new ProductDraft("Price product","price-product",category.Id,null,ProductKind.Simple,"","","",true,false,[],[],[new(null,"PRICE-1",[])],[],[],[]);
        var product=await Read<ProductView>(await b.Client.PostAsJsonAsync($"/api/v1/tenants/{b.Id}/catalog/products",draft),HttpStatusCode.Created);
        product=await Read<ProductView>(await b.Client.PostAsJsonAsync($"/api/v1/tenants/{b.Id}/catalog/products/{product.Id}/publish",new SRECommerce.Catalog.Application.VersionRequest(product.Version!)));
        var client=Factory.Client(b.Token);var path=Path(b.Id);
        await Read<JsonElement>(await client.PutAsJsonAsync(path+"/settings",new SRECommerce.Pricing.Application.SettingsRequest("INR",2)));
        var tax=await Read<JsonElement>(await client.PostAsJsonAsync(path+"/tax-policies",new TaxRequest("NONE","Explicit development zero-rate",0,false)),HttpStatusCode.Created);
        var list=await Read<JsonElement>(await client.PostAsJsonAsync(path+"/price-lists",new PriceListRequest("RETAIL","Retail","INR",PriceAudience.Retail,null,0,Factory.Clock.GetUtcNow().AddDays(-1),Factory.Clock.GetUtcNow().AddDays(30))),HttpStatusCode.Created);
        var variant=product.Variants[0].Id!.Value;var taxId=tax.GetProperty("id").GetGuid();var listId=list.GetProperty("id").GetGuid();
        await Read<ProductPriceView>(await client.PostAsJsonAsync(path+"/prices",new PriceRequest(listId,product.Id,variant,taxId,1,120,100,60)),HttpStatusCode.Created);
        return new(b,client,product.Id,variant,taxId,listId);
    }
    public async Task<string> Machine(string source)
    {
        using var scope=Catalog.Tenant.Identity.Factory.Services.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        if(!machineSecrets.TryGetValue(source,out var secret))
        {
            secret=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");machineSecrets[source]=secret;
            var descriptor=new OpenIddictApplicationDescriptor{ClientId=ServiceManifest.ClientId(source),ClientSecret=secret,ClientType=ClientTypes.Confidential};
            descriptor.Permissions.UnionWith([Permissions.Endpoints.Token,Permissions.GrantTypes.ClientCredentials]);foreach(var permission in ServiceManifest.Scopes(source))descriptor.Permissions.Add(Permissions.Prefixes.Scope+permission);await manager.CreateAsync(descriptor);
        }
        using var identity=Catalog.Tenant.Identity.Factory.Client();var response=await identity.PostAsync("/connect/token",new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","client_credentials"},{"client_id",ServiceManifest.ClientId(source)},{"client_secret",secret},{"scope","internal_pricing"}}));
        return(await Read<JsonElement>(response)).GetProperty("access_token").GetString()!;
    }
    public async Task<(Guid Id,HttpClient Client)> Customer(PricingBusiness business,string role="Customer")
    {
        var customer=await Catalog.Tenant.User(SRECommerce.Identity.Domain.AccountKind.Customer);using var identity=Catalog.Tenant.Identity.Factory.Client();identity.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",business.Catalog.Token);
        Assert.Equal(HttpStatusCode.NoContent,(await identity.PutAsJsonAsync($"/api/v1/tenants/{business.Catalog.Id}/memberships/{customer.Id}",new SRECommerce.Identity.Application.MembershipRequest(customer.Id,role))).StatusCode);
        var tokens=await Protocol.SignIn(Catalog.Tenant.Identity.Factory,customer.Email,customer.Password,business.Catalog.Id,scope:CatalogFixture.Scopes);return(customer.Id,Factory.Client(tokens.Access));
    }
    public void Dispose(){Factory.Dispose();Catalog.Dispose();}
}
