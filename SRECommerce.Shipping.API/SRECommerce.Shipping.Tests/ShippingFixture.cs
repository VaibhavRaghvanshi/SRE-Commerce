using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Order.Tests;
using SRECommerce.Payment.Tests;
using SRECommerce.Shipping.API;
using SRECommerce.Shipping.Application;
using SRECommerce.Shipping.Domain;
using SRECommerce.Shipping.Infrastructure;
using Xunit;
namespace SRECommerce.Shipping.Tests;

public sealed class LostShippingResponseHandler(HttpMessageHandler inner,ShippingFactory factory):DelegatingHandler(inner)
{
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
 {
  if(factory.OrderOffline)throw new HttpRequestException("Isolated Order outage");
  var response=await base.SendAsync(request,ct);
  if(request.Method==HttpMethod.Post&&request.RequestUri!.AbsolutePath.EndsWith("/status",StringComparison.Ordinal))
  {
   factory.UpdateRequests++;
   if(factory.LoseUpdateResponseOnce&&response.IsSuccessStatusCode){factory.LoseUpdateResponseOnce=false;response.Dispose();throw new HttpRequestException("Isolated response loss after Order commit");}
  }
  return response;
 }
}
public sealed class ShippingFactory(ShippingFixture fixture):WebApplicationFactory<ShippingProgram>
{
 public string DatabaseName{get;}="SRECommerce_ShippingTests_"+Guid.NewGuid().ToString("N");
 public bool CartOffline{get;set;}public bool OrderOffline{get;set;}public bool InventoryOffline{get;set;}public bool LoseUpdateResponseOnce{get;set;}public int UpdateRequests{get;set;}
 protected override void ConfigureWebHost(IWebHostBuilder builder)
 {
  var order=fixture.Payment.Order;
  builder.UseEnvironment("Development");builder.UseSetting("ConnectionStrings:Shipping",$"Server=localhost,1433;Database={DatabaseName};Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
  builder.UseSetting("Identity:BaseUrl","https://localhost/");builder.UseSetting("ServiceAuth:ClientId",ServiceManifest.ClientId("Shipping"));builder.UseSetting("ServiceAuth:ClientSecret",order.ClientSecret("Shipping"));builder.UseSetting("RateLimiting:PermitLimit","1000");
  builder.ConfigureServices(services=>
  {
   services.AddSingleton<TimeProvider>(order.Cart.Pricing.Factory.Clock);foreach(var descriptor in services.Where(s=>s.ServiceType==typeof(IHostedService)&&s.ImplementationType==typeof(ShippingCallbackWorker)).ToArray())services.Remove(descriptor);
   services.AddHttpClient("identity").ConfigurePrimaryHttpMessageHandler(()=>order.Cart.Pricing.Catalog.Tenant.Identity.Factory.Server.CreateHandler());services.AddHttpClient("service-identity").ConfigurePrimaryHttpMessageHandler(()=>order.Cart.Pricing.Catalog.Tenant.Identity.Factory.Server.CreateHandler());services.AddHttpClient("tenant").ConfigurePrimaryHttpMessageHandler(()=>order.Cart.Pricing.Catalog.Tenant.Factory.Server.CreateHandler());
   services.AddHttpClient("cart").ConfigurePrimaryHttpMessageHandler(()=>new SRECommerce.Catalog.Tests.DependencyTestHandler(order.Cart.Factory.Server.CreateHandler(),()=>CartOffline));services.AddHttpClient("inventory").ConfigurePrimaryHttpMessageHandler(()=>new SRECommerce.Catalog.Tests.DependencyTestHandler(order.Cart.Inventory.Server.CreateHandler(),()=>InventoryOffline));
   services.AddHttpClient("order").ConfigurePrimaryHttpMessageHandler(()=>new LostShippingResponseHandler(order.Factory.Server.CreateHandler(),this));
  });builder.ConfigureLogging(o=>o.SetMinimumLevel(LogLevel.Error));
 }
 public HttpClient Client(string? token=null,string? cartKey=null,string? orderKey=null)
 {
  var client=CreateClient(new WebApplicationFactoryClientOptions{BaseAddress=new Uri("https://localhost"),AllowAutoRedirect=false});if(token is not null)client.DefaultRequestHeaders.Authorization=new("Bearer",token);
  if(cartKey is not null)client.DefaultRequestHeaders.Add("X-Cart-Key",cartKey);if(orderKey is not null)client.DefaultRequestHeaders.Add("X-Order-Key",orderKey);return client;
 }
}
public sealed record ShippingBusiness(OrderBusiness Order,HttpClient Owner,HttpClient Customer,ZoneView Zone,MethodView Method,ShippingOption Option):IDisposable
{public Guid Tenant=>Order.Tenant;public void Dispose(){Owner.Dispose();Customer.Dispose();Order.Dispose();}}
public sealed class ShippingFixture:IDisposable
{
 public PaymentFixture Payment{get;}=new();public ShippingFactory Factory{get;}
 public ShippingFixture()
 {
  Factory=new(this);using var client=Factory.Client();using var scope=Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ShippingDbContext>();if(db.Database.GetDbConnection().Database!=Factory.DatabaseName||!Factory.DatabaseName.StartsWith("SRECommerce_ShippingTests_",StringComparison.Ordinal))throw new InvalidOperationException("Wrong Shipping test database");db.Database.Migrate();Payment.Order.ShippingHandler=()=>Factory.Server.CreateHandler();
 }
 public static string Path(Guid tenant,bool guest=false)=>$"/api/v1/{(guest?"public/":"")}tenants/{tenant}/shipping";
 public static string MachinePath(Guid tenant)=>$"/internal/v1/tenants/{tenant}/shipping";
 public static async Task<T> Read<T>(HttpResponseMessage response,HttpStatusCode expected=HttpStatusCode.OK)
 {Assert.True(response.StatusCode==expected,$"Expected {(int)expected}, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");return(await response.Content.ReadFromJsonAsync<T>())!;}
 public async Task<ShippingBusiness> Business(bool guest=false,DeliveryKind kind=DeliveryKind.LocalDelivery,string paymentMethod="Card",decimal flat=10,decimal perUnit=0,decimal tax=0,bool free=false)
 {
  var b=await Payment.Business(method:paymentMethod,guest:guest,free:free);var owner=Factory.Client(b.Pricing.Catalog.Token);
  var zone=await Read<ZoneView>(await owner.PostAsJsonAsync(Path(b.Tenant)+"/zones",new ZoneRequest(Guid.NewGuid(),"LOCAL","Local delivery zone",["IN"],["11"])));
  var method=await Read<MethodView>(await owner.PostAsJsonAsync(Path(b.Tenant)+"/methods",new MethodRequest(Guid.NewGuid(),zone.Id,"STANDARD","Standard delivery","Local development delivery",kind,"INR",flat,perUnit,tax,null,0,null,50000,kind==DeliveryKind.StorePickup?b.Warehouse:null)));
  var cartKey=guest?b.CartClient.DefaultRequestHeaders.GetValues("X-Cart-Key").Single():null;
  var customer=Factory.Client(guest?null:b.Pricing.Catalog.Token,cartKey,b.OrderKey);
  var options=await Read<ShippingOption[]>(await customer.PostAsJsonAsync(Path(b.Tenant,guest)+"/quotes/options",new QuoteOptionsRequest(Guid.NewGuid(),b.Preparation.Id,"IN","110001")));
  var option=Assert.Single(options);b=b with{Request=b.Request with{ShippingQuoteId=option.Id}};return new(b,owner,customer,zone,method,option);
 }
 public async Task Callbacks(Guid tenant){using var scope=Factory.Services.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenant,"Shipping");await scope.ServiceProvider.GetRequiredService<IShippingCallbacks>().Run(CancellationToken.None);}
 public async Task<ShipmentView> Shipment(ShippingBusiness business,decimal quantity=2)
 {
  await Payment.Order.Start(business.Order);var checkout=await Payment.Order.Drain(business.Order);Assert.Equal(SRECommerce.Order.Domain.CheckoutState.Accepted,checkout.State);
  var fulfillment=await Read<SRECommerce.Order.Application.FulfillmentView>(await business.Order.Owner.PostAsJsonAsync(OrderFixture.Manage(business.Tenant)+$"/orders/{checkout.Id}/fulfillments",new SRECommerce.Order.Application.FulfillOrderRequest(Guid.NewGuid(),checkout.Order!.Version,business.Order.Warehouse,[new(business.Order.Variant,quantity)])));
  await Payment.Order.Work(business.Tenant);var list=await business.Owner.GetFromJsonAsync<System.Text.Json.JsonElement>(Path(business.Tenant)+"/shipments");var shipmentId=Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("id").GetGuid();Assert.Equal(fulfillment.Id,shipmentId);
  return(await business.Owner.GetFromJsonAsync<System.Text.Json.JsonElement>(Path(business.Tenant)+$"/shipments/{shipmentId}/history")).GetProperty("shipment").Deserialize<ShipmentView>(ContractJson.Strict)!;
 }
 public async Task<ShipmentView> Move(ShippingBusiness b,ShipmentView shipment,ShipmentStatus state)=>await Read<ShipmentView>(await b.Owner.PostAsJsonAsync(Path(b.Tenant)+$"/shipments/{shipment.Id}/status",new ShipmentStateRequest(Guid.NewGuid(),shipment.Version,state,"Recorded local development movement")));
 public void Dispose(){Factory.Dispose();Payment.Dispose();}
}
