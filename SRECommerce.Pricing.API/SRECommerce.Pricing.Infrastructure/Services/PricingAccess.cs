using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Pricing.Application;
namespace SRECommerce.Pricing.Infrastructure;
public sealed class PricingAccess(ModuleAccess access,ModuleScope scope,RemoteAccess remote,IHttpContextAccessor contexts):IPricingAccess
{
    public UserAccess Actor=>access.User;
    public Task Authorize(Guid tenant,ActorPermission permission,CancellationToken ct)=>access.AuthorizeAsync(tenant,permission,ct);
    public async Task<PublicTenant> Tenant(Guid tenant,CancellationToken ct)
    {var state=await remote.PublicTenantAsync(tenant,ct);if(state is null||state.TenantId!=tenant||!state.Modules.Contains("pricing"))throw new BusinessFault("PricingTenantNotFound",404);return state;}
    public async Task<PublicTenant> Guest(Guid tenant,CancellationToken ct)
    {var value=await access.PublicAsync(tenant,ct);scope.BindGuest(tenant);return value;}
    public async Task Machine(Guid tenant,string[] sources,CancellationToken ct)
    {access.MachineAsync(tenant,sources);_=await Tenant(tenant,ct);}
    private void Delegate(HttpRequestMessage request)
    {request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",remote.UserBearer());var reason=contexts.HttpContext?.Request.Headers["X-Support-Reason"].ToString();if(!string.IsNullOrEmpty(reason))request.Headers.Add("X-Support-Reason",reason);}
    public async Task<CatalogPriceItem> CatalogItem(Guid tenant,Guid product,Guid variant,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/tenants/{tenant}/catalog/variants/{variant}");Delegate(request);using var response=await remote.Send("catalog",request,ct);
        if(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new BusinessFault("CatalogVariantUnavailable");if(!response.IsSuccessStatusCode)throw new DependencyFault("Catalog");
        try
        {
            using var document=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);var value=document.RootElement;
            if(value.GetProperty("id").GetGuid()!=product)throw new BusinessFault("CatalogVariantUnavailable");var sku=value.GetProperty("variants").EnumerateArray().SingleOrDefault(v=>v.GetProperty("id").GetGuid()==variant&&v.GetProperty("active").GetBoolean());if(sku.ValueKind==JsonValueKind.Undefined)throw new BusinessFault("CatalogVariantUnavailable");
            return new(product,variant,sku.GetProperty("sku").GetString()!,value.GetProperty("kind").GetInt32()==2);
        }
        catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){throw new DependencyFault("Catalog");}
    }
    public async Task<Guid[]> VisibleVariants(Guid tenant,Guid[] ids,bool publicRead,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,(publicRead?$"api/v1/public/tenants/{tenant}/catalog/variants?":$"api/v1/tenants/{tenant}/catalog/shopping-variants?")+string.Join("&",ids.Select(id=>"ids="+id)));
        if(!publicRead)Delegate(request);using var response=await remote.Send("catalog",request,ct);if(response.StatusCode==HttpStatusCode.NotFound)return[];if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new BusinessFault("CatalogAccessDenied",403);if(!response.IsSuccessStatusCode)throw new DependencyFault("Catalog");
        return await RemoteAccess.Read<Guid[]>(response,"Catalog",ct)??throw new DependencyFault("Catalog");
    }
    public async Task CustomerMember(Guid tenant,Guid user,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/tenants/{tenant}/memberships");Delegate(request);using var response=await remote.Send("identity",request,ct);
        if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new BusinessFault("CustomerMembershipCheckDenied",403);if(!response.IsSuccessStatusCode)throw new DependencyFault("Identity");
        try
        {
            using var document=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);
            if(!document.RootElement.EnumerateArray().Any(m=>m.GetProperty("userId").GetGuid()==user&&m.GetProperty("isActive").GetBoolean()&&m.GetProperty("role").GetString() is "Customer" or "B2BBuyer"))throw new BusinessFault("ActiveCustomerMembershipRequired");
        }
        catch(Exception error)when(error is JsonException or InvalidOperationException or KeyNotFoundException or FormatException){throw new DependencyFault("Identity");}
    }
}
