using System.Net;
using System.Net.Http.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Infrastructure;

public sealed class NotificationAccess(ModuleAccess access, ModuleScope scope) : INotificationAccess
{
    public async Task<Guid> Human(Guid tenant, ActorPermission permission, CancellationToken ct)
    { await access.AuthorizeAsync(tenant, permission, ct); return scope.User!.UserId; }
    public async Task Order(Guid tenant, CancellationToken ct)
    { await access.PublicAsync(tenant, ct); access.MachineAsync(tenant, "Order"); }
}

public sealed class OrderNotices(ServiceHttp http) : IOrderNotices
{
    public async Task<NoticeContext> Resolve(Guid tenant, OrderNoticeRequest notice, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/tenants/{tenant}/orders/{notice.OrderId}/notification-context?eventId={notice.EventId}&kind={(int)notice.Kind}");
        using var response = await http.Send("Order", request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) throw new BusinessFault("NotifiableOrderNotFound", 404);
        if (!response.IsSuccessStatusCode) throw new DependencyFault("Order");
        return await response.Content.ReadFromJsonAsync<NoticeContext>(ContractJson.Strict, ct) ?? throw new DependencyFault("Order");
    }
}
