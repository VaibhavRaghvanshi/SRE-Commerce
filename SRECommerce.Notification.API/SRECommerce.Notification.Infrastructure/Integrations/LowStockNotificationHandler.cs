using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Application;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Infrastructure;

public sealed class LowStockNotificationHandler(NotificationDbContext db, ModuleAccess access, ModuleScope scope, IManagerRecipients recipients) : IIntegrationHandler
{
    public IReadOnlyCollection<string> EventTypes => MessagingTopology.InventoryEvents;
    public async Task HandleAsync(EventEnvelope message, CancellationToken ct)
    {
        if (message.Producer != "Inventory" || !EventTypes.Contains(message.Type)) throw new BusinessFault("InvalidStockProducer", 400);
        var stock = message.Data.Deserialize<StockAlertEvent>(ContractJson.Strict) ?? throw new BusinessFault("InvalidStockAlert", 400);
        var now = DateTimeOffset.UtcNow;
        if (stock.VariantId == Guid.Empty || string.IsNullOrWhiteSpace(stock.Sku) || stock.Sku.Length > 64 ||
            stock.Available < 0 || stock.Available > stock.ReorderLevel || stock.Episode < 1 || stock.ObservedAt > now.AddMinutes(5))
            throw new BusinessFault("InvalidStockAlert", 400);
        var tenant = await access.PublicAsync(message.TenantId, ct);
        scope.BindEvent(message.TenantId, "Inventory");
        var subscriptions = tenant.Modules.Contains("inventory")
            ? await db.StockSubscriptions.AsNoTracking().Where(row => row.Enabled).Select(row => row.UserId).Take(100).ToArrayAsync(ct)
            : [];
        var contacts = new Dictionary<Guid, ManagerNotificationRecipient>();
        if (stock.ObservedAt >= now.AddDays(-1))
            foreach (var user in subscriptions)
                if (await recipients.Find(message.TenantId, user, ct) is {} contact) contacts.Add(user, contact);
        await IntegrationInbox.Apply(db, message, async token =>
        {
            var template = await db.Templates.SingleOrDefaultAsync(row => row.Kind == NotificationKind.LowStock &&
                row.Channel == NotificationChannel.Email && row.Enabled, token);
            if (template is null) return;
            var values = new Dictionary<string, string>
            {
                ["sku"] = stock.Sku,
                ["available"] = stock.Available.ToString("0.###", CultureInfo.InvariantCulture),
                ["reorderLevel"] = stock.ReorderLevel.ToString("0.###", CultureInfo.InvariantCulture)
            };
            foreach (var contact in contacts.Values)
            {
                var subscription = await db.StockSubscriptions.SingleOrDefaultAsync(row => row.UserId == contact.UserId, token);
                if (subscription is null || !subscription.Enabled) continue;
                if (await db.Preferences.AnyAsync(row => row.UserId == contact.UserId && !row.Email, token)) continue;
                var receipt = await db.StockReceipts.SingleOrDefaultAsync(row => row.UserId == contact.UserId && row.VariantId == stock.VariantId, token);
                if (receipt is not null && receipt.QueuedAt.AddMinutes(subscription.CooldownMinutes) > now) continue;
                if (receipt is null)
                {
                    receipt = new StockAlertReceipt { TenantId = message.TenantId, UserId = contact.UserId, VariantId = stock.VariantId };
                    db.StockReceipts.Add(receipt);
                }
                receipt.QueuedAt = now;
                var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes("stock:" + message.Id.ToString("N") + ":" + contact.UserId.ToString("N"))).AsSpan(0, 16));
                db.Jobs.Add(new NotificationJob
                {
                    TenantId = message.TenantId, EventId = id, ResourceId = stock.VariantId, UserId = contact.UserId,
                    Kind = NotificationKind.LowStock, Channel = NotificationChannel.Email, Recipient = contact.Email,
                    Subject = NotificationTemplateRules.Render(template.Subject, values), Body = NotificationTemplateRules.Render(template.Body, values),
                    State = DeliveryState.Pending, CreatedAt = now, NextAttemptAt = now
                });
            }
        }, ct);
    }
}
