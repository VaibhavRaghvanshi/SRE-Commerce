using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Cart.Domain;

namespace SRECommerce.Cart.Application;

public sealed partial class CartService
{
    public async Task<CartReminderPreference> Reminder(Guid tenant, Guid id, bool available, CancellationToken ct)
    {
        await AuthorizeReminder(tenant, ct);
        await Owned(id, false, null, ct);
        return ReminderPreference(available, await repository.Reminder(id, ct));
    }

    public async Task<CartReminderPreference> SetReminder(Guid tenant, Guid id, CartReminderRequest request, bool available, CancellationToken ct)
    {
        await AuthorizeReminder(tenant, ct);
        if (request.Enabled && !available) throw new BusinessFault("CartRemindersUnavailable", 503);

        return await repository.Transaction(async token =>
        {
            var cart = await Owned(id, false, null, token);
            var reminder = await repository.Reminder(id, token);
            if (request.Enabled)
            {
                if (reminder?.QueuedEventId is not null)
                {
                    if (reminder.Enabled) return ReminderPreference(available, reminder);
                    throw new BusinessFault("CartReminderAlreadyQueued");
                }
                if (!cart.Items.Any(item => item.Active)) throw new BusinessFault("CartEmpty", 400);
            }
            if (reminder is null && !request.Enabled) return ReminderPreference(available, null);
            if (reminder?.Enabled == request.Enabled) return ReminderPreference(available, reminder);
            if (reminder is null)
            {
                reminder = new CartReminder { TenantId = tenant, CartId = cart.Id };
                repository.Add(reminder);
            }
            reminder.Enabled = request.Enabled;
            if (request.Enabled) reminder.RequestedAt = Now;
            await repository.Commit(request.Enabled ? "cart.reminder.enabled" : "cart.reminder.cancelled", id, null, token);
            return ReminderPreference(available, reminder);
        }, ct);
    }

    private async Task AuthorizeReminder(Guid tenant, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ct);
        if (access.Actor?.TenantRole is not ("Customer" or "B2BBuyer")) throw new BusinessFault("CartReminderCustomerRequired", 403);
    }

    private static CartReminderPreference ReminderPreference(bool available, CartReminder? reminder) =>
        new(available, reminder?.Enabled == true, reminder?.QueuedEventId is not null);
}
