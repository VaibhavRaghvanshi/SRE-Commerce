using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Application;

public sealed class NotificationService(INotificationStore store, INotificationAccess access, IOrderNotices orders, TimeProvider clock)
{
    private static readonly Regex OrderToken = new(@"\{\{\s*orderNumber\s*\}\}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static string Version(byte[] value) => Convert.ToBase64String(value);
    private static TemplateView View(NotificationTemplate row) => new(row.Kind, row.Channel, row.Subject, row.Body, row.Enabled, Version(row.Version));
    private static PreferenceView View(NotificationPreference? row) => row is null ? new(true, false, false, null) : new(row.Email, row.Sms, row.WhatsApp, Version(row.Version));
    private static JobView View(NotificationJob row) => new(row.EventId, row.ResourceId, row.Kind, row.Channel, row.State, row.Attempts, row.CreatedAt);
    private static string TemplateText(string value, int limit, bool multiline)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit ||
            value.Any(c => char.IsControl(c) && !(multiline && c is '\r' or '\n' or '\t')))
            throw new BusinessFault("InvalidNotificationTemplate", 400);
        var literal = OrderToken.Replace(value, "");
        if (literal.Contains('{') || literal.Contains('}') || OrderToken.Count(value) > 1) throw new BusinessFault("UnknownNotificationTemplateToken", 400);
        return value.Trim();
    }

    public async Task<TemplateView[]> Templates(Guid tenant, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct);
        return (await store.Templates(ct)).Select(View).ToArray();
    }

    public async Task<TemplateView> SetTemplate(Guid tenant, NotificationKind kind, NotificationChannel channel, TemplateInput input, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct);
        if (!Enum.IsDefined(kind) || kind == NotificationKind.LowStock || !Enum.IsDefined(channel)) throw new BusinessFault("UnsupportedNotificationTemplate", 400);
        var subject = TemplateText(input.Subject, 160, false);
        var body = TemplateText(input.Body, 2000, true);
        return await store.Transaction(async token =>
        {
            var row = await store.Template(kind, channel, token);
            if (row is null)
            {
                if (input.Version is not null) throw new BusinessFault("VersionConflict");
                row = new() { TenantId = tenant, Kind = kind, Channel = channel };
                store.Add(row);
            }
            else store.Version(row, input.Version ?? throw new BusinessFault("VersionRequired", 400));
            row.Subject = subject; row.Body = body; row.Enabled = input.Enabled;
            await store.Save("notification.template.saved", $"{kind}:{channel}", token);
            return View(row);
        }, ct);
    }

    public async Task<PreferenceView> Preferences(Guid tenant, CancellationToken ct)
    {
        var user = await access.Human(tenant, ActorPermission.Customer, ct);
        return View(await store.Preference(user, ct));
    }

    public async Task<PreferenceView> SetPreferences(Guid tenant, PreferenceInput input, CancellationToken ct)
    {
        var user = await access.Human(tenant, ActorPermission.Customer, ct);
        return await store.Transaction(async token =>
        {
            var row = await store.Preference(user, token);
            if (row is null)
            {
                if (input.Version is not null) throw new BusinessFault("VersionConflict");
                row = new() { TenantId = tenant, UserId = user }; store.Add(row);
            }
            else store.Version(row, input.Version ?? throw new BusinessFault("VersionRequired", 400));
            row.Email = input.Email; row.Sms = input.Sms; row.WhatsApp = input.WhatsApp;
            await store.Save("notification.preferences.saved", user.ToString(), token);
            return View(row);
        }, ct);
    }

    public async Task<JobView[]> Jobs(Guid tenant, int page, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct);
        if (page is < 1 or > 1000) throw new BusinessFault("InvalidPage", 400);
        return (await store.Jobs(page, ct)).Select(View).ToArray();
    }

    public async Task<AttemptView[]> Attempts(Guid tenant, Guid id, NotificationChannel channel, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct);
        _ = await store.Job(id, channel, ct) ?? throw new BusinessFault("NotificationJobNotFound", 404);
        return (await store.Attempts(id, channel, ct)).Select(row => new AttemptView(row.Attempt, row.State, row.Code, row.RecordedAt)).ToArray();
    }

    public async Task<JobView[]> QueueOrder(Guid tenant, OrderNoticeRequest input, CancellationToken ct)
    {
        await access.Order(tenant, ct);
        if (input.EventId == Guid.Empty || input.OrderId == Guid.Empty || !Enum.IsDefined(input.Kind) || input.Kind == NotificationKind.LowStock)
            throw new BusinessFault("InvalidOrderNotice", 400);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(input))));
        async Task<JobView[]?> Replay(CancellationToken token)
        {
            var receipt = await store.Receipt(input.EventId, token);
            if (receipt is null) return null;
            if (receipt.Hash != hash) throw new BusinessFault("NotificationEventKeyReused");
            return (await store.ForEvent(input.EventId, token)).Select(View).ToArray();
        }
        if (await Replay(ct) is {} existing) return existing;
        var context = await orders.Resolve(tenant, input, ct);
        if (context.OrderId != input.OrderId) throw new DependencyFault("Order");
        return await store.Transaction(async token =>
        {
            if (await Replay(token) is {} replay) return replay;
            var preference = context.UserId is {} user ? await store.Preference(user, token) : null;
            var jobs = new List<NotificationJob>();
            foreach (var channel in Enum.GetValues<NotificationChannel>())
            {
                var template = await store.Template(input.Kind, channel, token);
                if (template is null || !template.Enabled) continue;
                var recipient = channel == NotificationChannel.Email ? context.Email : context.Phone;
                var permitted = (preference?.Permits(channel) ?? channel == NotificationChannel.Email) && !string.IsNullOrWhiteSpace(recipient);
                var now = clock.GetUtcNow();
                var job = new NotificationJob { TenantId = tenant, EventId = input.EventId, ResourceId = input.OrderId, UserId = context.UserId,
                    Kind = input.Kind, Channel = channel, Recipient = permitted ? recipient : null,
                    Subject = OrderToken.Replace(template.Subject, _ => context.OrderNumber), Body = OrderToken.Replace(template.Body, _ => context.OrderNumber),
                    State = permitted ? DeliveryState.Pending : DeliveryState.Suppressed, CreatedAt = now, NextAttemptAt = now };
                store.Add(job); jobs.Add(job);
            }
            store.AddReceipt(input.EventId, hash);
            await store.Save("notification.order.queued", input.EventId.ToString(), token);
            return jobs.Select(View).ToArray();
        }, ct);
    }
}
