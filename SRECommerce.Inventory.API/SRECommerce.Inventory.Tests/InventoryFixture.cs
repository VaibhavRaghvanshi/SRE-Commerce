using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.Catalog.Application;
using SRECommerce.Catalog.Domain;
using SRECommerce.Catalog.Tests;
using SRECommerce.Tenant.Application;
using SRECommerce.Inventory.API;
using SRECommerce.Inventory.Application;
using SRECommerce.Inventory.Infrastructure;
using Xunit;
namespace SRECommerce.Inventory.Tests;
public sealed class InventoryClock:TimeProvider
{
    private long ticks=DateTimeOffset.UtcNow.UtcTicks;
    public override DateTimeOffset GetUtcNow()=>new(Interlocked.Read(ref ticks),TimeSpan.Zero);
    public void Advance(TimeSpan interval)=>Interlocked.Add(ref ticks,interval.Ticks);
}
public sealed class InventoryFactory(CatalogFixture catalog):WebApplicationFactory<InventoryProgram>
{
    public string DatabaseName{get;}="SRECommerce_InventoryTests_"+Guid.NewGuid().ToString("N");
    public InventoryClock Clock{get;}=new();public bool CatalogOffline{get;set;}public bool TenantOffline{get;set;}public bool IdentityOffline{get;set;}
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");builder.UseSetting("ConnectionStrings:Inventory",$"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");builder.UseSetting("RateLimiting:PermitLimit","1000");
        builder.ConfigureServices(services=>
        {
            services.AddSingleton<TimeProvider>(Clock);
            foreach(var descriptor in services.Where(d=>d.ServiceType==typeof(IHostedService)&&d.ImplementationType==typeof(ReservationExpiryWorker)).ToArray())services.Remove(descriptor);
            services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(catalog.Tenant.Identity.Factory.Server.CreateHandler(),()=>IdentityOffline));
            services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(catalog.Tenant.Factory.Server.CreateHandler(),()=>TenantOffline));
            services.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(catalog.Factory.Server.CreateHandler(),()=>CatalogOffline));
        });builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));
    }
    public HttpClient Client(string? token=null)
    {var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});if(token is not null)client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);return client;}
}
public record InventoryBusiness(CatalogBusiness Catalog,HttpClient Client,Guid Warehouse,Guid Variant);
public sealed class InventoryFixture:IDisposable
{
    public CatalogFixture Catalog{get;}=new();public InventoryFactory Factory{get;}
    public InventoryFixture()
    {
        Factory=new(Catalog);using var client=Factory.Client();using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        if(db.Database.GetDbConnection().Database!=Factory.DatabaseName||!Factory.DatabaseName.StartsWith("SRECommerce_InventoryTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong inventory test database");db.Database.Migrate();
    }
    public static async Task<T> Read<T>(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK)
    {Assert.True(response.StatusCode==expected,$"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>())!;}
    public static string Path(Guid tenant)=>$"/api/v1/tenants/{tenant}/inventory";
    public async Task<InventoryBusiness> Business()
    {
        var b=await Catalog.Business();using var tenant=Catalog.Tenant.Factory.Client(b.Token);var current=(await tenant.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{b.Id}"))!;
        await Read<TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{b.Id}/modules/inventory",new ModuleRequest(current.Version,true)));
        var category=await Read<CatalogLookup>(await b.Client.PostAsJsonAsync($"/api/v1/tenants/{b.Id}/catalog/categories",new CategoryRequest("Inventory test","inventory-test",null)),HttpStatusCode.Created);
        var draft=new ProductDraft("Stock product","stock-product",category.Id,null,ProductKind.Simple,"","","",true,false,[],[],[new(null,"STOCK-1",[])],[],[],[]);
        var product=await Read<ProductView>(await b.Client.PostAsJsonAsync($"/api/v1/tenants/{b.Id}/catalog/products",draft),HttpStatusCode.Created);
        product=await Read<ProductView>(await b.Client.PostAsJsonAsync($"/api/v1/tenants/{b.Id}/catalog/products/{product.Id}/publish",new SRECommerce.Catalog.Application.VersionRequest(product.Version!)));
        var client=Factory.Client(b.Token);var path=Path(b.Id);await Read<System.Text.Json.JsonElement>(await client.PostAsJsonAsync(path+"/items",new RegisterItemRequest(product.Id,product.Variants[0].Id!.Value,2)));
        var branch=await Read<System.Text.Json.JsonElement>(await client.PostAsJsonAsync(path+"/branches",new BranchRequest("MAIN","Main branch")),HttpStatusCode.Created);
        var warehouse=await Read<System.Text.Json.JsonElement>(await client.PostAsJsonAsync(path+"/warehouses",new WarehouseRequest(branch.GetProperty("id").GetGuid(),"MAIN","Main warehouse")),HttpStatusCode.Created);
        return new(b,client,warehouse.GetProperty("id").GetGuid(),product.Variants[0].Id!.Value);
    }
    public void Dispose(){Factory.Dispose();Catalog.Dispose();}
}
