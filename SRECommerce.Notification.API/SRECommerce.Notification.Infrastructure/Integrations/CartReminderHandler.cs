using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Infrastructure;

public sealed class CartReminderHandler(NotificationDbContext db, RemoteAccess remote, IConfiguration configuration,
    IHostEnvironment environment, TimeProvider clock) : IIntegrationHandler
{
    public IReadOnlyCollection<string> EventTypes => [MessagingTopology.CartReminderDue];

    public async Task HandleAsync(EventEnvelope message, CancellationToken ct)
    {
        if (message.Producer != "Cart" || message.Type != MessagingTopology.CartReminderDue || message.SchemaVersion != 1)
            throw new BusinessFault("InvalidCartReminderProducer", 400);
        var reminder = message.Data.Deserialize<CartReminderEvent>(ContractJson.Strict) ?? throw new BusinessFault("InvalidCartReminder", 400);
        if (reminder.CartId == Guid.Empty || reminder.UserId == Guid.Empty) throw new BusinessFault("InvalidCartReminder", 400);
        var tenant = await remote.PublicTenantAsync(message.TenantId, ct);
        var now = clock.GetUtcNow();
        await IntegrationInbox.Apply(db, message, _ =>
        {
            if (tenant is null || tenant.TenantId != message.TenantId || !tenant.Modules.Contains("cart") || !tenant.Modules.Contains("notifications"))
                return Task.CompletedTask;
            var origin = StorefrontOrigin();
            var cartUrl = new Uri(origin, "stores/" + Uri.EscapeDataString(tenant.Slug) + "/cart");
            db.Jobs.Add(new NotificationJob
            {
                TenantId = message.TenantId, EventId = message.Id, ResourceId = reminder.CartId, UserId = reminder.UserId,
                Kind = NotificationKind.CartReminder, Channel = NotificationChannel.Email, State = DeliveryState.Pending,
                Subject = "A reminder about your saved cart", Body = "You asked for a reminder about your saved cart. Sign in to see your cart and current prices: " + cartUrl,
                CreatedAt = now, NextAttemptAt = now
            });
            return Task.CompletedTask;
        }, ct);
    }

    private Uri StorefrontOrigin()
    {
        var value = configuration["CartReminders:StorefrontOrigin"];
        if (value is null || value.Length > 512 || !Uri.TryCreate(value, UriKind.Absolute, out var origin) ||
            origin.AbsolutePath != "/" || origin.Query.Length != 0 || origin.Fragment.Length != 0 || origin.UserInfo.Length != 0 ||
            (origin.Scheme != "https" && !(environment.IsDevelopment() && origin.Scheme == "http" && origin.IsLoopback)))
            throw new DependencyFault("CartReminderStorefrontOrigin");
        return origin;
    }
}

public sealed class CartReminderDelivery(ServiceHttp http, RemoteAccess remote)
{
    public async Task<string?> EligibleRecipient(NotificationJob job, CancellationToken ct)
    {
        if (job.UserId is not {} user || job.Channel != NotificationChannel.Email) return null;
        var tenant = await remote.PublicTenantAsync(job.TenantId, ct);
        if (tenant is null || tenant.TenantId != job.TenantId || !tenant.Modules.Contains("cart") || !tenant.Modules.Contains("notifications")) return null;

        using var contactRequest = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/tenants/{job.TenantId}/cart-reminder-recipients/{user}");
        using var contactResponse = await http.Send("Identity", contactRequest, ct);
        if (contactResponse.StatusCode == HttpStatusCode.NotFound) return null;
        if (!contactResponse.IsSuccessStatusCode) throw new DependencyFault("Identity");
        var contact = await contactResponse.Content.ReadFromJsonAsync<CartReminderRecipient>(ContractJson.Strict, ct);
        if (contact is null || contact.UserId != user) throw new DependencyFault("Identity");

        using var cartRequest = new HttpRequestMessage(HttpMethod.Get,
            $"internal/v1/tenants/{job.TenantId}/cart/reminders/{job.EventId}/eligibility?cartId={job.ResourceId}&userId={user}");
        using var cartResponse = await http.Send("Cart", cartRequest, ct);
        if (cartResponse.StatusCode == HttpStatusCode.NotFound) return null;
        if (cartResponse.StatusCode != HttpStatusCode.NoContent) throw new DependencyFault("Cart");
        return contact.Email;
    }
}
