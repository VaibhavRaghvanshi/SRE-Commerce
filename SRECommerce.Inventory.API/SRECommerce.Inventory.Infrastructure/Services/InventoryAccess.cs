using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Inventory.Application;
namespace SRECommerce.Inventory.Infrastructure;
public sealed class InventoryAccess(ModuleAccess access,RemoteAccess remote,IHttpContextAccessor contexts):IInventoryAccess
{
    public Task Authorize(Guid tenant,ActorPermission permission,CancellationToken ct)=>access.AuthorizeAsync(tenant,permission,ct);
    public async Task Public(Guid tenant,CancellationToken ct)=>_=await access.PublicAsync(tenant,ct);
    public async Task<Guid[]> PublicVariants(Guid tenant,Guid[] ids,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/public/tenants/{tenant}/catalog/variants?"+string.Join("&",ids.Select(id=>"ids="+id)));
        using var response=await remote.Send("catalog",request,ct);if(response.StatusCode==HttpStatusCode.NotFound)return[];if(!response.IsSuccessStatusCode)throw new DependencyFault("Catalog");return await RemoteAccess.Read<Guid[]>(response,"Catalog",ct)??throw new DependencyFault("Catalog");
    }
    public async Task Machine(Guid tenant,string[] sources,CancellationToken ct)
    {
        access.MachineAsync(tenant,sources);var state=await remote.PublicTenantAsync(tenant,ct);
        if(state is null||state.TenantId!=tenant||!state.Modules.Contains("inventory"))throw new BusinessFault("ModuleUnavailable",403);
    }
    public async Task<CatalogStockItem> CatalogItem(Guid tenant,Guid product,Guid variant,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/tenants/{tenant}/catalog/variants/{variant}");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",remote.UserBearer());
        var reason=contexts.HttpContext?.Request.Headers["X-Support-Reason"].ToString();if(!string.IsNullOrEmpty(reason))request.Headers.Add("X-Support-Reason",reason);
        using var response=await remote.Send("catalog",request,ct);
        if(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new BusinessFault("CatalogVariantUnavailable");
        if(!response.IsSuccessStatusCode)throw new DependencyFault("Catalog");
        try
        {
            using var document=await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct),cancellationToken:ct);var value=document.RootElement;
            if(value.GetProperty("id").GetGuid()!=product||value.GetProperty("status").GetInt32()==2||value.GetProperty("publishedRevision").GetInt32()<1)throw new BusinessFault("PublishedCatalogProductRequired");
            var sku=value.GetProperty("variants").EnumerateArray().SingleOrDefault(v=>v.GetProperty("id").GetGuid()==variant&&v.GetProperty("active").GetBoolean());if(sku.ValueKind==JsonValueKind.Undefined)throw new BusinessFault("CatalogVariantUnavailable");
            return new(product,variant,sku.GetProperty("sku").GetString()!,sku.GetProperty("barcode").GetString());
        }
        catch(Exception ex)when(ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException){throw new DependencyFault("Catalog");}
    }
}
