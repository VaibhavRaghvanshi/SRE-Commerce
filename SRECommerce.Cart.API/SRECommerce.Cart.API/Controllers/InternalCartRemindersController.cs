using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Cart.Infrastructure;

namespace SRECommerce.Cart.API;

[ApiController, Authorize(Policy = "Machine")]
[Route("internal/v1/tenants/{tenant:guid}/cart/reminders")]
public sealed class InternalCartRemindersController(ModuleAccess access, CartDbContext db, TimeProvider clock, IConfiguration configuration) : ControllerBase
{
    [HttpGet("{eventId:guid}/eligibility")]
    public async Task<IActionResult> Eligible(Guid tenant, Guid eventId, [FromQuery] Guid cartId, [FromQuery] Guid userId, CancellationToken ct)
    {
        access.MachineAsync(tenant, "Notification");
        if (!configuration.GetValue<bool>("CartReminders:Enabled")) return NotFound();
        var now = clock.GetUtcNow();
        var eligible = await CartReminderEligibility.Due(db, now).AnyAsync(row => row.CartId == cartId && row.Cart.UserId == userId &&
            row.QueuedEventId == eventId && row.QueuedAt > now.AddDays(-1), ct);
        return eligible ? NoContent() : NotFound();
    }
}
