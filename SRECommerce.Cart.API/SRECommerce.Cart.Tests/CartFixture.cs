using System.Net;using System.Net.Http.Headers;using System.Net.Http.Json;using System.Security.Cryptography;using System.Text.Json;
using Microsoft.AspNetCore.Hosting;using Microsoft.AspNetCore.Mvc.Testing;using Microsoft.EntityFrameworkCore;using Microsoft.Extensions.DependencyInjection;using Microsoft.Extensions.Logging;using OpenIddict.Abstractions;
using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Cart.API;using SRECommerce.Cart.Application;using SRECommerce.Cart.Infrastructure;using SRECommerce.Catalog.Tests;using SRECommerce.Inventory.Tests;using SRECommerce.Inventory.Infrastructure;using SRECommerce.Pricing.Tests;using SRECommerce.Tenant.Application;using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;
namespace SRECommerce.Cart.Tests;
public sealed class CartFactory(PricingFixture pricing,InventoryFactory inventory):WebApplicationFactory<CartProgram>
{
 public string DatabaseName{get;}="SRECommerce_CartTests_"+Guid.NewGuid().ToString("N");public bool IdentityOffline{get;set;}public bool TenantOffline{get;set;}public bool CatalogOffline{get;set;}public bool InventoryOffline{get;set;}public bool PricingOffline{get;set;}
 protected override void ConfigureWebHost(IWebHostBuilder builder)
 {
  builder.UseEnvironment("Development");builder.UseSetting("ConnectionStrings:Cart",$"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");builder.UseSetting("RateLimiting:PermitLimit","1000");
  builder.ConfigureServices(services=>{
   services.AddSingleton<TimeProvider>(pricing.Factory.Clock);
   services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(pricing.Catalog.Tenant.Identity.Factory.Server.CreateHandler(),()=>IdentityOffline));
   services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(pricing.Catalog.Tenant.Factory.Server.CreateHandler(),()=>TenantOffline));
   services.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(pricing.Catalog.Factory.Server.CreateHandler(),()=>CatalogOffline));
   services.AddHttpClient("inventory").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(inventory.Server.CreateHandler(),()=>InventoryOffline));
   services.AddHttpClient("pricing").ConfigurePrimaryHttpMessageHandler(()=>new DependencyTestHandler(pricing.Factory.Server.CreateHandler(),()=>PricingOffline));
  });builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));
 }
 public HttpClient Client(string? token=null,string? capability=null)
 {var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});if(token is not null)client.DefaultRequestHeaders.Authorization=new("Bearer",token);if(capability is not null)client.DefaultRequestHeaders.Add("X-Cart-Key",capability);return client;}
}
public sealed class CartFixture:IDisposable
{
 public PricingFixture Pricing{get;}=new();public InventoryFactory Inventory{get;}public CartFactory Factory{get;}private readonly Dictionary<string,string> secrets=[];
 public CartFixture()
 {
  Inventory=new(Pricing.Catalog);using var inventoryClient=Inventory.Client();using(var scope=Inventory.Services.CreateScope()){
   var db=scope.ServiceProvider.GetRequiredService<InventoryDbContext>();if(db.Database.GetDbConnection().Database!=Inventory.DatabaseName||!Inventory.DatabaseName.StartsWith("SRECommerce_InventoryTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong Inventory test database");db.Database.Migrate();
  }
  Factory=new(Pricing,Inventory);using var client=Factory.Client();using var cartScope=Factory.Services.CreateScope();var cartDb=cartScope.ServiceProvider.GetRequiredService<CartDbContext>();if(cartDb.Database.GetDbConnection().Database!=Factory.DatabaseName||!Factory.DatabaseName.StartsWith("SRECommerce_CartTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong Cart test database");cartDb.Database.Migrate();
 }
 public static string Key()=>Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+','-').Replace('/','_');
 public static string Path(Guid tenant,bool guest=false)=>$"/api/v1/{(guest?"public/":"")}tenants/{tenant}/cart/carts";
 public static async Task<T> Read<T>(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK){Assert.True(response.StatusCode==expected,$"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>())!;}
 public async Task<PricingBusiness> Business(decimal opening=10)
 {
  var business=await Pricing.Business();using var tenant=Pricing.Catalog.Tenant.Factory.Client(business.Catalog.Token);
  foreach(var module in new[]{"inventory","cart"}){var current=(await tenant.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{business.Catalog.Id}"))!;await Read<TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{business.Catalog.Id}/modules/{module}",new ModuleRequest(current.Version,true)));}
  using var inventory=Inventory.Client(business.Catalog.Token);var path=$"/api/v1/tenants/{business.Catalog.Id}/inventory";
  await Read<JsonElement>(await inventory.PostAsJsonAsync(path+"/items",new SRECommerce.Inventory.Application.RegisterItemRequest(business.Product,business.Variant)));
  var branch=await Read<JsonElement>(await inventory.PostAsJsonAsync(path+"/branches",new SRECommerce.Inventory.Application.BranchRequest("MAIN","Main")),HttpStatusCode.Created);
  var warehouse=await Read<JsonElement>(await inventory.PostAsJsonAsync(path+"/warehouses",new SRECommerce.Inventory.Application.WarehouseRequest(branch.GetProperty("id").GetGuid(),"MAIN","Main")),HttpStatusCode.Created);
  if(opening>0)await Read<JsonElement>(await inventory.PostAsJsonAsync(path+"/stock-changes",new SRECommerce.Inventory.Application.StockChangeRequest(Guid.NewGuid(),warehouse.GetProperty("id").GetGuid(),business.Variant,SRECommerce.Inventory.Domain.StockAction.Receipt,opening,"Test opening")));
  return business;
 }
 public async Task<string> Machine(string source)
 {
  using var scope=Pricing.Catalog.Tenant.Identity.Factory.Services.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
  if(!secrets.TryGetValue(source,out var secret)){secret=Key()+Key();secrets[source]=secret;var descriptor=new OpenIddictApplicationDescriptor{ClientId=ServiceManifest.ClientId(source),ClientSecret=secret,ClientType=ClientTypes.Confidential};descriptor.Permissions.UnionWith([Permissions.Endpoints.Token,Permissions.GrantTypes.ClientCredentials]);foreach(var value in ServiceManifest.Scopes(source))descriptor.Permissions.Add(Permissions.Prefixes.Scope+value);await manager.CreateAsync(descriptor);}
  using var identity=Pricing.Catalog.Tenant.Identity.Factory.Client();return(await Read<JsonElement>(await identity.PostAsync("/connect/token",new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","client_credentials"},{"client_id",ServiceManifest.ClientId(source)},{"client_secret",secret},{"scope","internal_cart"}})))).GetProperty("access_token").GetString()!;
 }
 public void Dispose(){Factory.Dispose();Inventory.Dispose();Pricing.Dispose();}
}
