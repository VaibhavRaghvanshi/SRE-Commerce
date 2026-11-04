using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Application;

public interface INotificationStore
{
    Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task<NotificationTemplate?> Template(NotificationKind kind, NotificationChannel channel, CancellationToken ct);
    Task<NotificationTemplate[]> Templates(CancellationToken ct);
    Task<NotificationPreference?> Preference(Guid user, CancellationToken ct);
    Task<NotificationJob[]> Jobs(int page, CancellationToken ct);
    Task<NotificationJob[]> ForEvent(Guid id, CancellationToken ct);
    Task<NotificationJob?> Job(Guid id, NotificationChannel channel, CancellationToken ct);
    Task<NotificationDelivery[]> Attempts(Guid id, NotificationChannel channel, CancellationToken ct);
    Task<InboxReceipt?> Receipt(Guid id, CancellationToken ct);
    void AddReceipt(Guid id, string hash);
    void Add<T>(T row) where T : class;
    void Version(SRECommerce.BuildingBlocks.Kernel.IVersionedRecord row, string version);
    Task Save(string action, string resource, CancellationToken ct);
    Task<StockAlertSubscription?> StockSubscription(Guid user, CancellationToken ct);
}

public sealed record InboxReceipt(string Hash);

public interface INotificationAccess
{
    Task<Guid> Human(Guid tenant, ActorPermission permission, CancellationToken ct);
    Task Order(Guid tenant, CancellationToken ct);
    Task Event(Guid tenant, string producer, CancellationToken ct);
}

public interface IOrderNotices
{
    Task<NoticeContext> Resolve(Guid tenant, OrderNoticeRequest request, CancellationToken ct);
}

public interface INotificationTransport
{
    bool Available { get; }
    Task<DeliveryResult> Send(NotificationJob job, CancellationToken ct);
}
public interface IManagerRecipients
{
    Task<ManagerNotificationRecipient?> Find(Guid tenant, Guid user, CancellationToken ct);
}
