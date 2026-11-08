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
public sealed class MembershipsController(IdentityDbContext db, IIdentityAccounts accounts, UserManager<ApplicationUser> users,
    MembershipCapacity capacity) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    private bool Selected(Guid tenantId) => Guid.TryParse(User.FindFirstValue("tenant_id"), out var selected) && selected == tenantId;
    private Task<bool> IsOwner(CancellationToken ct) => db.Memberships.AsNoTracking()
        .AnyAsync(m => m.UserId == UserId && m.IsActive && m.Role == "Owner", ct);
    private async Task<bool> Permitted(Guid tenantId, string permission, CancellationToken ct)
    {
        if (!Selected(tenantId)) return false;
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == UserId && m.IsActive, ct);
        return member is not null && await db.RolePermissions.AnyAsync(p => p.Role == member.Role && p.Permission == permission, ct);
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken ct) => await Permitted(tenantId, "members.read", ct)
        ? Ok(await (from membership in db.Memberships.AsNoTracking() join profile in db.Profiles.AsNoTracking() on membership.UserId equals profile.UserId select new {membership.UserId, membership.Role, membership.IsActive, DisplayName=profile.FullName}).ToListAsync(ct)) : Forbid();

    [HttpGet("directory")]
    public async Task<IActionResult> Directory(Guid tenantId, [FromQuery] int page = 1, [FromQuery] string? search = null,
        [FromQuery] string? role = null, [FromQuery] bool? active = null, CancellationToken ct = default)
    {
        if (!await Permitted(tenantId, "members.read", ct)) return Forbid();
        var term = search?.Trim();
        if (page is < 1 or > 10000 || term is { Length: > 100 } || term?.Any(char.IsControl) == true ||
            role is not null && !TenantRoles.All.Contains(role))
            return BadRequest(new { error = "InvalidMembershipDirectoryQuery" });

        var roster = from member in db.Memberships.AsNoTracking()
                     join profile in db.Profiles.AsNoTracking() on member.UserId equals profile.UserId
                     select new { member.UserId, member.Role, member.IsActive, member.Version, DisplayName = profile.FullName };
        if (role is not null) roster = roster.Where(member => member.Role == role);
        if (active is not null) roster = roster.Where(member => member.IsActive == active);
        if (!string.IsNullOrEmpty(term)) roster = roster.Where(member => member.DisplayName.Contains(term));
        const int pageSize = 25;
        var items = await roster.OrderBy(member => member.DisplayName).ThenBy(member => member.UserId)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(ct);
        return Ok(new { items, total = await roster.CountAsync(ct), page, pageSize, activeCount = await capacity.CountAsync(tenantId, ct) });
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Assign(Guid tenantId, Guid userId, MembershipRequest request, CancellationToken ct)
    {
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await capacity.LockAsync(tenantId, ct);
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        if (request.UserId != userId || request.Role is not ("Manager" or "Staff" or "Customer" or "B2BBuyer")) return BadRequest(new { error = "Owner assignments require the platform ownership workflow." });
        var existing = await db.Memberships.SingleOrDefaultAsync(m => m.UserId == userId, ct);
        if (existing?.Role == "Owner") return Forbid();
        if ((existing?.Role == "Manager" || request.Role == "Manager") && !await IsOwner(ct)) return Forbid();
        if (existing is { IsActive: true } && existing.Role == request.Role) return NoContent();
        if (request.Version is not null && (existing is null || Convert.ToBase64String(existing.Version) != request.Version))
            return Conflict(new { error = "MembershipVersionConflict" });
        var target = await users.FindByIdAsync(userId.ToString());
        if (target is null) return NotFound();
        if (request.Role == "Manager" && (!target.EmailConfirmed || !await db.Profiles.AnyAsync(p => p.UserId == userId && p.IsActive, ct)))
            return BadRequest(new { error = "An active, email-confirmed account is required for Manager access." });
        if (existing is null || !existing.IsActive)
        {
            var seat = await capacity.CheckAsync(tenantId, ct);
            if (seat == MembershipSeat.Full) return Conflict(new { error = "MembershipLimitReached" });
            if (seat == MembershipSeat.PlanInactive) return StatusCode(403, new { error = "MembershipPlanInactive" });
        }
        if (existing is null) db.Memberships.Add(new TenantMembership { TenantId = tenantId, UserId = userId, Role = request.Role });
        else { existing.Role = request.Role; existing.IsActive = true; }
        await accounts.RevokeTenantSessionsAsync(userId, tenantId, ct);
        await accounts.AuditAsync(UserId, tenantId, "membership.assign", $"success:{userId:D}:{request.Role}", ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Deactivate(Guid tenantId, Guid userId, [FromQuery] string? version, CancellationToken ct)
    {
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        await capacity.LockAsync(tenantId, ct);
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        var existing = await db.Memberships.SingleOrDefaultAsync(m => m.UserId == userId, ct);
        if (existing is null) return NotFound();
        if (existing.Role == "Owner" || existing.Role == "Manager" && !await IsOwner(ct)) return Forbid();
        if (!existing.IsActive) return NoContent();
        if (version is not null && Convert.ToBase64String(existing.Version) != version)
            return Conflict(new { error = "MembershipVersionConflict" });
        existing.IsActive = false;
        await accounts.RevokeTenantSessionsAsync(userId, tenantId, ct);
        await accounts.AuditAsync(UserId, tenantId, "membership.deactivate", $"success:{userId:D}:{existing.Role}", ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
}
