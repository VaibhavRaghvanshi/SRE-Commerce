using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Application;

public sealed record TemplateInput(string Subject, string Body, bool Enabled, string? Version);
public sealed record PreferenceInput(bool Email, bool Sms, bool WhatsApp, string? Version);
public sealed record PreferenceView(bool Email, bool Sms, bool WhatsApp, string? Version);
public sealed record TemplateView(NotificationKind Kind, NotificationChannel Channel, string Subject, string Body, bool Enabled, string Version);
public sealed record OrderNoticeRequest(Guid EventId, Guid OrderId, NotificationKind Kind);
public sealed record NoticeContext(Guid OrderId, Guid? UserId, string OrderNumber, string Email, string? Phone);
public sealed record JobView(Guid EventId, Guid ResourceId, NotificationKind Kind, NotificationChannel Channel, DeliveryState State, int Attempts, DateTimeOffset CreatedAt);
public sealed record AttemptView(int Attempt, DeliveryState State, string Code, DateTimeOffset RecordedAt);
public sealed record DeliveryResult(DeliveryState State, string Code, bool Retry = false);
