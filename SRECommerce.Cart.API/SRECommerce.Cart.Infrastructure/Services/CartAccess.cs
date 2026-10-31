using System.Net;using System.Net.Http.Headers;using System.Net.Http.Json;using Microsoft.AspNetCore.Http;
using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Cart.Application;
namespace SRECommerce.Cart.Infrastructure;
public sealed class CartAccess(ModuleAccess access,ModuleScope scope,RemoteAccess remote,IHttpContextAccessor contexts):ICartAccess
{
 public UserAccess? Actor=>scope.User;
 public async Task Authorize(Guid tenant,bool guest,CancellationToken ct){if(guest){await access.PublicAsync(tenant,ct);scope.BindGuest(tenant);}else await access.AuthorizeAsync(tenant,ActorPermission.Customer,ct);}
 public async Task Machine(Guid tenant,CancellationToken ct){access.MachineAsync(tenant,"Order");var state=await remote.PublicTenantAsync(tenant,ct);if(state is null||!state.Modules.Contains("cart"))throw new BusinessFault("CartUnavailable",403);}
 private void Delegate(HttpRequestMessage request,bool guest)
 {if(guest)return;request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",remote.UserBearer());var reason=contexts.HttpContext?.Request.Headers["X-Support-Reason"].ToString();if(!string.IsNullOrEmpty(reason))request.Headers.Add("X-Support-Reason",reason);}
 private static string Prefix(Guid tenant,bool guest)=>guest?$"api/v1/public/tenants/{tenant}/":$"api/v1/tenants/{tenant}/";
 private async Task<T> Send<T>(string service,HttpRequestMessage request,CancellationToken ct)
 {
  using var response=await remote.Send(service,request,ct);
  if(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.BadRequest)throw new BusinessFault("Cart"+char.ToUpperInvariant(service[0])+service[1..]+"Rejected",(int)response.StatusCode);
  if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new BusinessFault("CartDependencyAccessDenied",403);
  if(!response.IsSuccessStatusCode)throw new DependencyFault(service);return await RemoteAccess.Read<T>(response,service,ct)??throw new DependencyFault(service);
 }
 public async Task<CartCatalogProduct[]> Products(Guid tenant,Guid[] variants,bool guest,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,Prefix(tenant,guest)+"catalog/shopping-products?"+string.Join("&",variants.Select(id=>"ids="+id)));Delegate(request,guest);return await Send<CartCatalogProduct[]>("catalog",request,ct);
 }
 public async Task<CartStock[]> Availability(Guid tenant,Guid[] variants,bool guest,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Get,Prefix(tenant,guest)+"inventory/availability?"+string.Join("&",variants.Select(id=>"variants="+id)));Delegate(request,guest);return await Send<CartStock[]>("inventory",request,ct);
 }
 public async Task<CartPriceQuote> Quote(Guid tenant,Guid id,CartQuoteItem[] items,string? coupon,bool guest,CancellationToken ct)
 {
  using var request=new HttpRequestMessage(HttpMethod.Post,Prefix(tenant,guest)+"pricing/quotes"){Content=JsonContent.Create(new{id,items,CouponCode=coupon})};Delegate(request,guest);return await Send<CartPriceQuote>("pricing",request,ct);
 }
}
