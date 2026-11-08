using System.Net;
using System.Text.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Identity.Application;

namespace SRECommerce.Identity.API;

public sealed class HttpMembershipPlans(ServiceHttp services) : IMembershipPlans
{
    public async Task<MembershipPlan?> FindAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/tenants/{tenantId}/membership-limits");
            using var response = await services.Send("Tenant", request, ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return null;
            if (!response.IsSuccessStatusCode) throw new MembershipPlanUnavailableException();
            var limit = await response.Content.ReadFromJsonAsync<ProvisioningLimits>(cancellationToken: ct);
            if (limit is null || limit.TenantId != tenantId || limit.MaxUsers is < 1 or > 100000)
                throw new MembershipPlanUnavailableException();
            return new(limit.Allowed, limit.MaxUsers);
        }
        catch (Exception error) when (error is DependencyFault or HttpRequestException or JsonException)
        {
            throw new MembershipPlanUnavailableException();
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new MembershipPlanUnavailableException();
        }
    }
}
