using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using SRECommerce.BuildingBlocks.Contracts;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public sealed class RemoteAccess(IHttpClientFactory clients,IHttpContextAccessor contexts)
{
    public string UserBearer()
    {
        if(!AuthenticationHeaderValue.TryParse(contexts.HttpContext?.Request.Headers.Authorization.ToString(),out var header)||!header.Scheme.Equals("Bearer",StringComparison.OrdinalIgnoreCase)||string.IsNullOrEmpty(header.Parameter))throw new BusinessFault("BearerRequired",401);
        return header.Parameter;
    }
    public Task<UserAccess?> UserAsync(string token,CancellationToken ct)=>GetAccess<UserAccess>("api/v1/me/access",token,ct);
    public Task<MachineAccess?> MachineAsync(string token,CancellationToken ct)=>GetAccess<MachineAccess>("internal/v1/access",token,ct);
    private async Task<T?> GetAccess<T>(string path,string token,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,path);request.Headers.Authorization=new("Bearer",token);
        using var response=await Send("identity",request,ct);
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)return default;
        if(!response.IsSuccessStatusCode)throw new DependencyFault("Identity");return await Read<T>(response,"Identity",ct);
    }
    public async Task<TenantAccess> EntitlementsAsync(Guid tenant,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/access/tenants/{tenant}/entitlements");request.Headers.Authorization=new("Bearer",UserBearer());
        using var response=await Send("tenant",request,ct);
        if(response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)throw new BusinessFault("TenantAccessDenied",403);
        if(response.StatusCode==HttpStatusCode.NotFound)throw new BusinessFault("TenantNotFound",404);
        if(!response.IsSuccessStatusCode)throw new DependencyFault("Tenant");return await Read<TenantAccess>(response,"Tenant",ct)??throw new DependencyFault("Tenant");
    }
    public async Task<PublicTenant?> PublicTenantAsync(Guid tenant,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"api/v1/public/tenants/by-id/{tenant}");using var response=await Send("tenant",request,ct);
        if(response.StatusCode==HttpStatusCode.NotFound)return null;if(!response.IsSuccessStatusCode)throw new DependencyFault("Tenant");
        return await Read<PublicTenant>(response,"Tenant",ct);
    }
    public async Task<HttpResponseMessage> Send(string client,HttpRequestMessage request,CancellationToken ct)
    {
        try{return await clients.CreateClient(client).SendAsync(request,ct);}
        catch(HttpRequestException){throw new DependencyFault(client);}
        catch(Polly.Timeout.TimeoutRejectedException){throw new DependencyFault(client);}
        catch(Polly.CircuitBreaker.BrokenCircuitException){throw new DependencyFault(client);}
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new DependencyFault(client);}
    }
    public static async Task<T?> Read<T>(HttpResponseMessage response,string dependency,CancellationToken ct)
    {
        try{return await response.Content.ReadFromJsonAsync<T>(cancellationToken:ct);}
        catch(JsonException){throw new DependencyFault(dependency);}
    }
    public static Uri BaseUri(IConfiguration config,string key,string defaultValue,bool development)
    {
        var uri=new Uri(config[key]??defaultValue);
        if(!uri.IsAbsoluteUri||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Query)||!uri.AbsoluteUri.EndsWith('/'))throw new InvalidOperationException("Invalid service base URI");
        if(uri.Scheme!="https" && (!development||!ServiceManifest.All.Select(s=>s.Name.ToLowerInvariant()).Concat(["localhost","127.0.0.1","host.docker.internal"]).Contains(uri.Host)))throw new InvalidOperationException("Service HTTPS required outside local development");
        return uri;
    }
}
public sealed class ModuleAccess(ServiceSpec module,RemoteAccess remote,IHttpContextAccessor contexts,ModuleScope scope,IModuleStore store)
{
    public UserAccess User=>contexts.HttpContext?.Items[typeof(UserAccess)] as UserAccess??throw new BusinessFault("UserRequired",401);
    public MachineAccess Machine=>contexts.HttpContext?.Items[typeof(MachineAccess)] as MachineAccess??throw new BusinessFault("ServiceIdentityRequired",401);
    public async Task AuthorizeAsync(Guid tenant,ActorPermission permission,CancellationToken ct)
    {
        var user=User;string? reason=null;
        if(user.IsPlatformAdmin)
        {reason=contexts.HttpContext?.Request.Headers["X-Support-Reason"].ToString().Trim();if(string.IsNullOrWhiteSpace(reason)||reason.Length>300||reason.Any(char.IsControl))throw new BusinessFault("SupportReasonRequired",403);}
        else
        {
            if(user.SelectedTenantId!=tenant)throw new BusinessFault("SelectedTenantRequired",403);
            var allowed=permission switch{ActorPermission.Owner=>user.TenantRole=="Owner",ActorPermission.Manager=>user.TenantRole is "Owner" or "Manager",ActorPermission.Staff=>user.TenantRole is "Owner" or "Manager" or "Staff",_=>user.TenantRole is "Owner" or "Manager" or "Staff" or "Customer" or "B2BBuyer"};
            if(!allowed)throw new BusinessFault("PermissionDenied",403);
        }
        var state=await remote.EntitlementsAsync(tenant,ct);
        if(state.TenantId!=tenant||!state.Active||!state.Modules.Any(m=>m.Module==module.Module&&m.Enabled))throw new BusinessFault("ModuleUnavailable",403);
        scope.BindUser(tenant,user,reason);
        if(reason is not null){store.Record(scope,contexts,"support.access",contexts.HttpContext?.Request.Path.ToString()??"support");await store.Context.SaveChangesAsync(ct);}
    }
    public async Task<PublicTenant> PublicAsync(Guid tenant,CancellationToken ct)
    {var state=await remote.PublicTenantAsync(tenant,ct);if(state is null||state.TenantId!=tenant||!state.Modules.Contains(module.Module))throw new BusinessFault("PublishedTenantNotFound",404);scope.BindPublic(tenant);return state;}
    public void MachineAsync(Guid tenant,params string[] permittedSources)
    {var actor=Machine;if(!actor.Scopes.Contains(module.InternalScope)||!permittedSources.Contains(actor.Service))throw new BusinessFault("ServicePermissionDenied",403);scope.BindMachine(tenant,actor);}
    public void GlobalPlatform(string action)
    {var actor=User;var reason=contexts.HttpContext?.Request.Headers["X-Support-Reason"].ToString().Trim();if(!actor.IsPlatformAdmin||string.IsNullOrWhiteSpace(reason)||reason.Length>300)throw new BusinessFault("PlatformReasonRequired",403);scope.BindGlobal(actor,reason);store.Record(scope,contexts,action,"global-catalog");}
}
