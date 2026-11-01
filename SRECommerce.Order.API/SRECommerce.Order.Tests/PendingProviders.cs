using System.Net;using System.Net.Http.Json;using System.Text.Json;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.Order.Application;
namespace SRECommerce.Order.Tests;
// Isolated contract doubles until Payment and Shipping are implemented; never registered by the production API.
public sealed class PendingProviders
{
 internal readonly object Gate=new();internal readonly Dictionary<Guid,ShippingQuoteSnapshot> Quotes=[];internal readonly Dictionary<Guid,RemotePayment> Payments=[];internal readonly HashSet<Guid> Aborted=[];internal readonly Dictionary<Guid,(Guid Order,decimal Amount)> Refunds=[];internal readonly HashSet<Guid> Shipments=[];
 public bool DeclinePayment {get;set;}public bool PaymentOffline {get;set;}public bool ShippingOffline {get;set;}public int Captures {get;internal set;}
 public ShippingQuoteSnapshot Quote(Guid tenant,OrderQuantity[] items,DateTimeOffset now){var value=new ShippingQuoteSnapshot(Guid.NewGuid(),tenant,Guid.NewGuid(),"Isolated test delivery","INR","IN","110001",items,10,0,10,now.AddMinutes(5),null);lock(Gate)Quotes[value.Id]=value;return value;}
 internal static string Version(int value)=>Convert.ToBase64String(BitConverter.GetBytes((long)value));
}
public sealed class PendingProviderHandler(OrderFixture fixture,string service):HttpMessageHandler
{
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
 {
  if(service=="Payment"&&fixture.Providers.PaymentOffline||service=="Shipping"&&fixture.Providers.ShippingOffline)throw new HttpRequestException("Isolated provider outage");
  using(var identity=fixture.Cart.Pricing.Catalog.Tenant.Identity.Factory.Client())
  {identity.DefaultRequestHeaders.Authorization=request.Headers.Authorization;var response=await identity.GetAsync("/internal/v1/access",ct);if(!response.IsSuccessStatusCode)return Error(401);var actor=(await response.Content.ReadFromJsonAsync<MachineAccess>(ct))!;if(actor.Service!="Order"||!actor.Scopes.Contains("internal_"+service.ToLowerInvariant()))return Error(403);}
  var path=request.RequestUri!.AbsolutePath.Split('/',StringSplitOptions.RemoveEmptyEntries);if(path.Length<5||!Guid.TryParse(path[3],out var tenant))return Error(404);
  if(service=="Shipping")
  {
   if(request.Method==HttpMethod.Get&&path.Length==7&&path[5]=="quotes"&&Guid.TryParse(path[6],out var quote))
   {lock(fixture.Providers.Gate)return fixture.Providers.Quotes.TryGetValue(quote,out var value)&&value.TenantId==tenant?Ok(value):Error(404);}
   if(request.Method==HttpMethod.Post&&path[5]=="shipments"){var body=await Body(request,ct);lock(fixture.Providers.Gate)fixture.Providers.Shipments.Add(body.GetProperty("fulfillmentId").GetGuid());return Ok(new{created=true});}
   return Error(404);
  }
  var payload=request.Content is null?default:await Body(request,ct);var orderId=path.Length>=7&&path[5]=="orders"?Guid.Parse(path[6]):payload.GetProperty("orderId").GetGuid();
  if(request.Method==HttpMethod.Get){lock(fixture.Providers.Gate)return fixture.Providers.Payments.TryGetValue(orderId,out var value)?Ok(value):Error(404);}
  RemotePayment? current;lock(fixture.Providers.Gate)fixture.Providers.Payments.TryGetValue(orderId,out current);
  if(current is null)
  {
   using var order=fixture.Factory.Client(await fixture.Token("Payment","internal_order"));var context=await OrderFixture.Read<MachineOrderContext>(await order.GetAsync($"/internal/v1/tenants/{tenant}/orders/{orderId}/context",ct));
   current=new(orderId,orderId,context.Currency,context.Total,context.PaymentMethod,fixture.Providers.DeclinePayment?4:context.PaymentMethod=="COD"?7:1,0,PendingProviders.Version(1));
   lock(fixture.Providers.Gate){if(fixture.Providers.Payments.TryGetValue(orderId,out var existing))current=existing;else fixture.Providers.Payments[orderId]=current;}
  }
  lock(fixture.Providers.Gate)
  {
   if(path.Length==5)return Ok(current);
   var operation=path[7];
   if(operation=="abort"){fixture.Providers.Aborted.Add(orderId);if(current.Status is 0 or 1 or 7)current=current with{Status=3,Version=PendingProviders.Version(2)};}
   else if(operation=="capture")
   {if(fixture.Providers.Aborted.Contains(orderId))return Error(409);if(current.Status is 1 or 7){fixture.Providers.Captures++;current=current with{Status=2,Version=PendingProviders.Version(3)};}else if(current.Status!=2)return Error(409);}
   else if(operation=="refunds")
   {
    var id=payload.GetProperty("id").GetGuid();var amount=payload.GetProperty("amount").GetDecimal();if(fixture.Providers.Refunds.TryGetValue(id,out var prior)){if(prior.Order!=orderId||prior.Amount!=amount)return Error(409);return Ok(current);}
    if(current.Status is not(2 or 5)||amount<=0||current.Refunded+amount>current.Amount)return Error(409);fixture.Providers.Refunds[id]=(orderId,amount);current=current with{Refunded=current.Refunded+amount,Status=current.Refunded+amount==current.Amount?6:5,Version=PendingProviders.Version(4)};
   }
   else return Error(404);
   fixture.Providers.Payments[orderId]=current;return Ok(current);
  }
 }
 private static async Task<JsonElement> Body(HttpRequestMessage request,CancellationToken ct)=>await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken:ct);
 private static HttpResponseMessage Ok(object value)=>new(HttpStatusCode.OK){Content=JsonContent.Create(value)};private static HttpResponseMessage Error(int status)=>new((HttpStatusCode)status){Content=JsonContent.Create(new{title="Isolated provider rejected request"})};
}
