using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Infrastructure;

namespace SRECommerce.Identity.API;

[ApiController, Authorize(Policy="ActiveSession")]
[Route("api/v1/tenants/{tenantId:guid}/memberships")]
public sealed class MembershipsController(IdentityDbContext db, IIdentityAccounts accounts, UserManager<ApplicationUser> users) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    private Task<bool> IsOwner(CancellationToken ct) => db.Memberships.AsNoTracking()
        .AnyAsync(m => m.UserId == UserId && m.IsActive && m.Role == "Owner", ct);
    private async Task<bool> Permitted(Guid tenantId, string permission, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var selected) || selected != tenantId) return false;
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == UserId && m.IsActive, ct);
        return member is not null && await db.RolePermissions.AnyAsync(p => p.Role == member.Role && p.Permission == permission, ct);
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken ct) => await Permitted(tenantId, "members.read", ct)
        ? Ok(await (from membership in db.Memberships.AsNoTracking() join profile in db.Profiles.AsNoTracking() on membership.UserId equals profile.UserId select new {membership.UserId, membership.Role, membership.IsActive, DisplayName=profile.FullName}).ToListAsync(ct)) : Forbid();

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Assign(Guid tenantId, Guid userId, MembershipRequest request, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        if (request.UserId != userId || request.Role is not ("Manager" or "Staff" or "Customer" or "B2BBuyer")) return BadRequest(new { error = "Owner assignments require the platform ownership workflow." });
        var existing = await db.Memberships.SingleOrDefaultAsync(m => m.UserId == userId, ct);
        if (existing?.Role == "Owner") return Forbid();
        if ((existing?.Role == "Manager" || request.Role == "Manager") && !await IsOwner(ct)) return Forbid();
        var target = await users.FindByIdAsync(userId.ToString());
        if (target is null) return NotFound();
        if (request.Role == "Manager" && (!target.EmailConfirmed || !await db.Profiles.AnyAsync(p => p.UserId == userId && p.IsActive, ct)))
            return BadRequest(new { error = "An active, email-confirmed account is required for Manager access." });
        if (existing is null) db.Memberships.Add(new TenantMembership { TenantId = tenantId, UserId = userId, Role = request.Role });
        else { existing.Role = request.Role; existing.IsActive = true; }
        await accounts.RevokeTenantSessionsAsync(userId, tenantId, ct);
        await accounts.AuditAsync(UserId, tenantId, "membership.assign", $"success:{userId:D}:{request.Role}", ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Deactivate(Guid tenantId, Guid userId, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        var existing = await db.Memberships.SingleOrDefaultAsync(m => m.UserId == userId, ct);
        if (existing is null) return NotFound();
        if (existing.Role == "Owner" || existing.Role == "Manager" && !await IsOwner(ct)) return Forbid();
        existing.IsActive = false;
        await accounts.RevokeTenantSessionsAsync(userId, tenantId, ct);
        await accounts.AuditAsync(UserId, tenantId, "membership.deactivate", $"success:{userId:D}:{existing.Role}", ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
}
