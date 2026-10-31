using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Infrastructure;

namespace SRECommerce.Identity.API;

[ApiController, Authorize(Policy="ActiveSession", Roles="SuperAdmin")]
[Route("api/v1/platform")]
public sealed class PlatformController(IdentityDbContext db, IIdentityAccounts accounts, UserManager<ApplicationUser> users) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    private async Task<bool> CurrentAdmin() => await users.FindByIdAsync(UserId.ToString()) is { } user && await users.IsInRoleAsync(user, "SuperAdmin");

    [HttpPut("tenants/{tenantId:guid}/memberships/{userId:guid}")]
    public async Task<IActionResult> Provision(Guid tenantId, Guid userId, MembershipRequest request, CancellationToken ct)
    {
        if (!await CurrentAdmin()) return Forbid();
        if (tenantId == Guid.Empty || request.UserId != userId || !TenantRoles.All.Contains(request.Role)) return BadRequest();
        var targetUser = await users.FindByIdAsync(userId.ToString());
        if (targetUser is null) return NotFound();
        if (request.Role == "Owner" && (!targetUser.EmailConfirmed || !await db.Profiles.AnyAsync(p=>p.UserId==userId && p.IsActive,ct)))
            return BadRequest(new { error = "An active, email-confirmed owner is required." });
        await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var member = await db.Memberships.IgnoreQueryFilters().SingleOrDefaultAsync(m => m.TenantId == tenantId && m.UserId == userId, ct);
        var membershipChanged = member is null || !member.IsActive || member.Role != request.Role;
        if (member is null) db.Memberships.Add(new TenantMembership { TenantId = tenantId, UserId = userId, Role = request.Role });
        else
        {
            if (member.Role == "Owner" && request.Role != "Owner"
                && await db.Memberships.IgnoreQueryFilters().CountAsync(m => m.TenantId == tenantId && m.Role == "Owner" && m.IsActive, ct) <= 1)
                return BadRequest(new { error = "The tenant must retain an active owner." });
            member.Role = request.Role; member.IsActive = true;
        }
        if (!await db.RolePermissions.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId, ct))
            foreach (var role in TenantRoles.All)
                foreach (var permission in TenantRoles.Permissions(role)) db.RolePermissions.Add(new RolePermission { TenantId = tenantId, Role = role, Permission = permission });
        if (membershipChanged) await accounts.RevokeTenantSessionsAsync(userId, tenantId, ct);
        await accounts.AuditAsync(UserId, tenantId, "platform.membership.provision", "explicit-privileged-operation", ct);
        await transaction.CommitAsync(ct);
        return NoContent();
    }
    [HttpPut("accounts/{userId:guid}/activation")]
    public async Task<IActionResult> Activation(Guid userId, ActivationRequest request, CancellationToken ct)
    {
        if (!await CurrentAdmin()) return Forbid();
        var profile = await db.Profiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null) return NotFound();
        if (profile.Kind == AccountKind.SuperAdmin) return BadRequest(new { error = "Platform administrator lifecycle requires a separate reviewed provisioning operation." });
        profile.IsActive = request.IsActive;
        await accounts.RevokeSessionsAsync(userId, null, ct);
        await accounts.AuditAsync(UserId, null, "platform.account.activation", "explicit-privileged-operation", ct);
        return NoContent();
    }
    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] Guid? tenantId, [FromQuery] int page = 1, CancellationToken ct = default)
    {
        if (!await CurrentAdmin()) return Forbid();
        if (page < 1 || page > 10000) return BadRequest();
        await accounts.AuditAsync(UserId, tenantId, "platform.audit.read", "explicit-privileged-operation", ct);
        return Ok(await db.AuditLogs.AsNoTracking().Where(a => tenantId == null || a.TenantId == tenantId).OrderByDescending(a => a.Id).Skip((page-1)*50).Take(50).ToListAsync(ct));
    }
}
