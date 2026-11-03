using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Notification.Domain;

public enum NotificationChannel { Email, Sms, WhatsApp }
public enum DeliveryState { Pending, Sending, Sent, Suppressed, Failed, NeedsReview }
public enum NotificationKind { OrderConfirmed, PaymentCaptured, OrderDispatched, OrderDelivered, OrderCancelled, LowStock }

public sealed class NotificationTemplate : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public NotificationKind Kind { get; set; }
    public NotificationChannel Channel { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public bool Enabled { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class NotificationPreference : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public bool Email { get; set; } = true;
    public bool Sms { get; set; }
    public bool WhatsApp { get; set; }
    public byte[] Version { get; set; } = [];
    public bool Permits(NotificationChannel channel) => channel switch
    {
        NotificationChannel.Email => Email,
        NotificationChannel.Sms => Sms,
        NotificationChannel.WhatsApp => WhatsApp,
        _ => false
    };
}

public sealed class NotificationJob : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public Guid ResourceId { get; set; }
    public Guid? UserId { get; set; }
    public NotificationKind Kind { get; set; }
    public NotificationChannel Channel { get; set; }
    public string? Recipient { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DeliveryState State { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public byte[] Version { get; set; } = [];
}

public sealed class NotificationDelivery : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid EventId { get; set; }
    public NotificationChannel Channel { get; set; }
    public int Attempt { get; set; }
    public DeliveryState State { get; set; }
    public string Code { get; set; } = "";
    public DateTimeOffset RecordedAt { get; set; }
}
