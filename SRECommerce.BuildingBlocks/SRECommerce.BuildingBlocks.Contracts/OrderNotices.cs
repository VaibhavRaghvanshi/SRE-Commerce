namespace SRECommerce.BuildingBlocks.Contracts;

public static class OrderNoticesContract
{
    // These outbox rows are delivered to the Notification API by the dedicated HTTP bridge.
    public const string Prefix = "order.notice.";
    public static readonly string[] Types = [
        "order.notice.confirmed.v1", "order.notice.paid.v1", "order.notice.dispatched.v1",
        "order.notice.delivered.v1", "order.notice.cancelled.v1"
    ];
}

public sealed record OrderNoticeResource(Guid OrderId);
