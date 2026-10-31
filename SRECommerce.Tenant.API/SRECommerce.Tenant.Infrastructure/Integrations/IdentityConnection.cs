using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DnsClient;
using Microsoft.AspNetCore.Http;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;
public sealed class IdentityConnection(HttpClient http,IHttpContextAccessor contexts) : IIdentityOwnerProvisioner
{
    public async Task<IdentityAccess?> AccessAsync(string token,CancellationToken ct)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,"api/v1/me/access");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        using var response=await Send(request,ct);
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return null;
        if(!response.IsSuccessStatusCode) throw new IdentityUnavailableException();
        try { return await response.Content.ReadFromJsonAsync<IdentityAccess>(ct) ?? throw new IdentityUnavailableException(); }
        catch(JsonException) { throw new IdentityUnavailableException(); }
    }
    public async Task ProvisionOwnerAsync(Guid tenantId,Guid userId,CancellationToken ct)
    {
        var authorization=contexts.HttpContext?.Request.Headers.Authorization.ToString();
        if(!AuthenticationHeaderValue.TryParse(authorization,out var header) || !header.Scheme.Equals("Bearer",StringComparison.OrdinalIgnoreCase)) throw new TenantRuleException("DelegatedAdministratorRequired",403);
        using var request=new HttpRequestMessage(HttpMethod.Put,$"api/v1/platform/tenants/{tenantId}/memberships/{userId}") {Content=JsonContent.Create(new{userId,role="Owner"})};
        request.Headers.Authorization=header;using var response=await Send(request,ct);
        if(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new TenantRuleException("OwnerProvisioningDenied",403);
        if(response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict) throw new TenantRuleException("OwnerProvisioningUnavailable",409);
        if(!response.IsSuccessStatusCode) throw new IdentityUnavailableException();
    }
    private async Task<HttpResponseMessage> Send(HttpRequestMessage request,CancellationToken ct)
    {
        try { return await http.SendAsync(request,ct); }
        catch(HttpRequestException) { throw new IdentityUnavailableException(); }
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) { throw new IdentityUnavailableException(); }
    }
}
