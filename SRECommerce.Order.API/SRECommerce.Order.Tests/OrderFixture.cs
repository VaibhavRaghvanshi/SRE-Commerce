using System.Net;using System.Net.Http.Headers;using System.Net.Http.Json;using System.Security.Cryptography;using System.Text.Json;
using Microsoft.AspNetCore.Hosting;using Microsoft.AspNetCore.Mvc.Testing;using Microsoft.EntityFrameworkCore;using Microsoft.Extensions.DependencyInjection;using Microsoft.Extensions.Hosting;using Microsoft.Extensions.Logging;using OpenIddict.Abstractions;
using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Cart.Tests;using SRECommerce.Catalog.Tests;using SRECommerce.Order.API;using SRECommerce.Order.Application;using SRECommerce.Order.Infrastructure;using SRECommerce.Pricing.Tests;using SRECommerce.Tenant.Application;using Xunit;
using static OpenIddict.Abstractions.OpenIddictConstants;
namespace SRECommerce.Order.Tests;
public sealed class OrderFactory(OrderFixture fixture):WebApplicationFactory<OrderProgram>
{
 public string DatabaseName{get;}="SRECommerce_OrderTests_"+Guid.NewGuid().ToString("N");
 protected override void ConfigureWebHost(IWebHostBuilder builder)
 {
  builder.UseEnvironment("Development");builder.UseSetting("ConnectionStrings:Order",$"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");builder.UseSetting("Identity:BaseUrl","https://localhost/");builder.UseSetting("ServiceAuth:ClientId",ServiceManifest.ClientId("Order"));builder.UseSetting("ServiceAuth:ClientSecret",fixture.OrderSecret);builder.UseSetting("RateLimiting:PermitLimit","1000");
  builder.ConfigureServices(services=>
  {
   services.AddSingleton<TimeProvider>(fixture.Cart.Pricing.Factory.Clock);foreach(var descriptor in services.Where(d=>d.ServiceType==typeof(IHostedService)&&d.ImplementationType==typeof(OrderRecoveryWorker)).ToArray())services.Remove(descriptor);
   services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(()=>fixture.Cart.Pricing.Catalog.Tenant.Identity.Factory.Server.CreateHandler());services.AddHttpClient("service-identity").ConfigurePrimaryHttpMessageHandler(()=>fixture.Cart.Pricing.Catalog.Tenant.Identity.Factory.Server.CreateHandler());
   services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(()=>fixture.Cart.Pricing.Catalog.Tenant.Factory.Server.CreateHandler());services.AddHttpClient("catalog").ConfigurePrimaryHttpMessageHandler(()=>fixture.Cart.Pricing.Catalog.Factory.Server.CreateHandler());services.AddHttpClient("cart").ConfigurePrimaryHttpMessageHandler(()=>fixture.Cart.Factory.Server.CreateHandler());services.AddHttpClient("inventory").ConfigurePrimaryHttpMessageHandler(()=>fixture.Cart.Inventory.Server.CreateHandler());services.AddHttpClient("pricing").ConfigurePrimaryHttpMessageHandler(()=>fixture.Cart.Pricing.Factory.Server.CreateHandler());
   services.AddHttpClient("payment").ConfigurePrimaryHttpMessageHandler(()=>fixture.PaymentHandler?.Invoke()??new PendingProviderHandler(fixture,"Payment"));services.AddHttpClient("shipping").ConfigurePrimaryHttpMessageHandler(()=>fixture.ShippingHandler?.Invoke()??new PendingProviderHandler(fixture,"Shipping"));
  });builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));
 }
 public HttpClient Client(string? token=null,string? orderKey=null,string? cartKey=null)
 {var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});if(token is not null)client.DefaultRequestHeaders.Authorization=new("Bearer",token);if(orderKey is not null)client.DefaultRequestHeaders.Add("X-Order-Key",orderKey);if(cartKey is not null)client.DefaultRequestHeaders.Add("X-Cart-Key",cartKey);return client;}
}
public sealed record OrderBusiness(PricingBusiness Pricing,HttpClient Client,HttpClient Owner,HttpClient CartClient,Guid Warehouse,SRECommerce.Cart.Application.CartView Cart,SRECommerce.Cart.Application.PreparedCartView Preparation,CheckoutRequest Request,bool Guest,string? OrderKey):IDisposable
{
 public Guid Tenant=>Pricing.Catalog.Id;public Guid Variant=>Pricing.Variant;public void Dispose(){Client.Dispose();Owner.Dispose();CartClient.Dispose();Pricing.Client.Dispose();Pricing.Catalog.Client.Dispose();}
}
public sealed class OrderFixture:IDisposable
{
 public CartFixture Cart{get;}=new();public OrderFactory Factory{get;}public PendingProviders Providers{get;}=new();internal string OrderSecret{get;}=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));private readonly Dictionary<string,string> secrets=[];
 public Func<HttpMessageHandler>? PaymentHandler{get;set;}public Func<HttpMessageHandler>? ShippingHandler{get;set;}
 public string ClientSecret(string source)=>secrets[source];
 public OrderFixture()
 {
  Register("Order",OrderSecret).GetAwaiter().GetResult();foreach(var source in new[]{"Payment","Shipping"})Register(source,Convert.ToHexString(RandomNumberGenerator.GetBytes(32))).GetAwaiter().GetResult();Factory=new(this);using var client=Factory.Client();using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<OrderDbContext>();if(db.Database.GetDbConnection().Database!=Factory.DatabaseName||!Factory.DatabaseName.StartsWith("SRECommerce_OrderTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong Order test database");db.Database.Migrate();
 }
 private async Task Register(string source,string secret)
 {using var scope=Cart.Pricing.Catalog.Tenant.Identity.Factory.Services.CreateScope();var manager=scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();var descriptor=new OpenIddictApplicationDescriptor{ClientId=ServiceManifest.ClientId(source),ClientSecret=secret,ClientType=ClientTypes.Confidential};descriptor.Permissions.UnionWith([Permissions.Endpoints.Token,Permissions.GrantTypes.ClientCredentials]);foreach(var permission in ServiceManifest.Scopes(source))descriptor.Permissions.Add(Permissions.Prefixes.Scope+permission);await manager.CreateAsync(descriptor);secrets[source]=secret;}
 public async Task<string> Token(string source,string scope)
 {if(!secrets.TryGetValue(source,out var secret)){secret=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));await Register(source,secret);}using var identity=Cart.Pricing.Catalog.Tenant.Identity.Factory.Client();return(await Read<JsonElement>(await identity.PostAsync("/connect/token",new FormUrlEncodedContent(new Dictionary<string,string>{{"grant_type","client_credentials"},{"client_id",ServiceManifest.ClientId(source)},{"client_secret",secret},{"scope",scope}})))).GetProperty("access_token").GetString()!;}
 public static string Path(Guid tenant,bool guest=false)=>$"/api/v1/{(guest?"public/":"")}tenants/{tenant}/orders";public static string Manage(Guid tenant)=>$"/api/v1/tenants/{tenant}/order-management";
 public static async Task<T> Read<T>(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK){Assert.True(response.StatusCode==expected,$"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>())!;}
 public async Task<OrderBusiness> Business(bool guest=false,decimal quantity=2,string method="Card")
 {
  var business=await Cart.Business();using var tenant=Cart.Pricing.Catalog.Tenant.Factory.Client(business.Catalog.Token);
  foreach(var module in new[]{"orders","payments","shipping"}){var state=(await tenant.GetFromJsonAsync<TenantView>($"/api/v1/tenants/{business.Catalog.Id}"))!;await Read<TenantView>(await tenant.PutAsJsonAsync($"/api/v1/tenants/{business.Catalog.Id}/modules/{module}",new ModuleRequest(state.Version,true)));}
  var owner=Factory.Client(business.Catalog.Token);await Read<JsonElement>(await owner.PutAsJsonAsync(Manage(business.Catalog.Id)+"/policy",new OrderPolicyRequest(true,true,true,14,1000000)));
  var cartKey=guest?CartFixture.Key():null;var orderKey=guest?CartFixture.Key():null;var cartClient=Cart.Factory.Client(guest?null:business.Catalog.Token,cartKey);var cartPath=CartFixture.Path(business.Catalog.Id,guest);
  var cart=await Read<SRECommerce.Cart.Application.CartView>(await cartClient.PostAsJsonAsync(cartPath,new SRECommerce.Cart.Application.CreateCartRequest(Guid.NewGuid())));cart=await Read<SRECommerce.Cart.Application.CartView>(await cartClient.PutAsJsonAsync(cartPath+$"/{cart.Id}/items",new SRECommerce.Cart.Application.CartItemRequest(Guid.NewGuid(),business.Variant,quantity,cart.Version)));
  var prepared=await Read<SRECommerce.Cart.Application.PreparedCartView>(await cartClient.PostAsJsonAsync(cartPath+$"/{cart.Id}/prepare",new SRECommerce.Cart.Application.PrepareCartRequest(Guid.NewGuid(),cart.Version)));
  using var inventory=Cart.Inventory.Client(business.Catalog.Token);var lookup=await inventory.GetFromJsonAsync<JsonElement>($"/api/v1/tenants/{business.Catalog.Id}/inventory/lookups");var warehouse=lookup.GetProperty("warehouses")[0].GetProperty("id").GetGuid();var quote=Providers.Quote(business.Catalog.Id,prepared.Items.Select(i=>new OrderQuantity(i.VariantId,i.Quantity)).ToArray(),Cart.Pricing.Factory.Clock.GetUtcNow());
  var request=new CheckoutRequest(Guid.NewGuid(),prepared.Id,quote.Id,method,new("Order test buyer","buyer@srecommerce.test",null,"Local sandbox delivery",null,"Delhi","IN","110001"));return new(business,Factory.Client(guest?null:business.Catalog.Token,orderKey,cartKey),owner,cartClient,warehouse,cart,prepared,request,guest,orderKey);
 }
 public async Task<CheckoutView> Start(OrderBusiness b)=>await Read<CheckoutView>(await b.Client.PostAsJsonAsync(Path(b.Tenant,b.Guest)+"/checkout",b.Request),HttpStatusCode.Accepted);
 public async Task<CheckoutView> Drain(OrderBusiness b,int maximum=35)
 {
  for(var index=0;index<maximum;index++)
  {
   await Step(b);Cart.Pricing.Factory.Clock.Advance(TimeSpan.FromSeconds(2));
   var result=(await b.Client.GetFromJsonAsync<CheckoutView>(Path(b.Tenant,b.Guest)+$"/checkouts/{b.Request.Id}"))!;if(result.State is Domain.CheckoutState.Accepted or Domain.CheckoutState.Failed or Domain.CheckoutState.Cancelled or Domain.CheckoutState.ManualReview)return result;
  }
  using var diagnostic=Factory.Services.CreateScope();diagnostic.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Tenant,"Order");var intent=await diagnostic.ServiceProvider.GetRequiredService<OrderDbContext>().Intents.SingleAsync(i=>i.Id==b.Request.Id);throw new InvalidOperationException($"Checkout stalled: {intent.State}/{intent.Phase}, {intent.LastFault}, attempts={intent.Attempts}");
 }
 public async Task Step(OrderBusiness b){using var scope=Factory.Services.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Tenant,"Order");await scope.ServiceProvider.GetRequiredService<IOrderSaga>().Run(b.Tenant,b.Request.Id,null,CancellationToken.None);}
 public async Task Work(Guid tenant){using var scope=Factory.Services.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenant,"Order");await scope.ServiceProvider.GetRequiredService<IOrderWorkflows>().Run(CancellationToken.None);}
 public void Dispose(){Factory.Dispose();Cart.Dispose();}
}
