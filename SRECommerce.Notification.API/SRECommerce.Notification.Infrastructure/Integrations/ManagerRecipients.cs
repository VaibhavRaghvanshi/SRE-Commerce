using System.Net;
using System.Net.Http.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Application;

namespace SRECommerce.Notification.Infrastructure;

public sealed class ManagerRecipients(ServiceHttp http) : IManagerRecipients
{
    public async Task<ManagerNotificationRecipient?> Find(Guid tenant, Guid user, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/tenants/{tenant}/notification-recipients/{user}");
        using var response = await http.Send("Identity", request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        if (!response.IsSuccessStatusCode) throw new DependencyFault("Identity");
        var recipient = await response.Content.ReadFromJsonAsync<ManagerNotificationRecipient>(ContractJson.Strict, ct);
        if (recipient is null || recipient.UserId != user) throw new DependencyFault("Identity");
        return recipient;
    }
}
