using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Notification.Application;

namespace SRECommerce.Notification.Infrastructure;

public sealed class OrderNotificationHandler(NotificationService service) : IIntegrationHandler
{
    public IReadOnlyCollection<string> EventTypes => MessagingTopology.OrderEvents;
    public Task HandleAsync(EventEnvelope message, CancellationToken ct) => service.QueueEvent(message, ct);
}
