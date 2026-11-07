using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using SRECommerce.Identity.Infrastructure;

namespace SRECommerce.Identity.API;

[ApiController, Authorize(Policy = "ServiceAccess")]
public sealed class NotificationRecipientsController(IdentityDbContext db) : ControllerBase
{
    [HttpGet("/internal/v1/tenants/{tenant:guid}/notification-recipients/{id:guid}")]
    public async Task<IActionResult> Recipient(Guid tenant, Guid id, CancellationToken ct)
        => await RecipientForRoles(tenant, id, ["Owner", "Manager"], ct);

    [HttpGet("/internal/v1/tenants/{tenant:guid}/cart-reminder-recipients/{id:guid}")]
    public async Task<IActionResult> CartRecipient(Guid tenant, Guid id, CancellationToken ct)
        => await RecipientForRoles(tenant, id, ["Customer", "B2BBuyer"], ct);

    private async Task<IActionResult> RecipientForRoles(Guid tenant, Guid id, string[] roles, CancellationToken ct)
    {
        if (User.GetClaim("service_name") != "Notification" || !User.HasScope("internal_identity")) return Forbid();
        if (tenant == Guid.Empty || id == Guid.Empty) return NotFound();
        var contact = await (
            from member in db.Memberships.IgnoreQueryFilters().AsNoTracking()
            join user in db.Users.AsNoTracking() on member.UserId equals user.Id
            join profile in db.Profiles.AsNoTracking() on member.UserId equals profile.UserId
            where member.TenantId == tenant && member.UserId == id && member.IsActive &&
                roles.Contains(member.Role) && profile.IsActive &&
                user.EmailConfirmed && user.Email != null && (user.LockoutEnd == null || user.LockoutEnd <= DateTimeOffset.UtcNow)
            select new { UserId = user.Id, Email = user.Email! }
        ).SingleOrDefaultAsync(ct);
        return contact is null ? NotFound() : Ok(contact);
    }
}
