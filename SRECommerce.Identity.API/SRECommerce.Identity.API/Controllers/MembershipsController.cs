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
    private async Task<bool> Permitted(Guid tenantId, string permission, CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue("tenant_id"), out var selected) || selected != tenantId) return false;
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.UserId == UserId && m.IsActive, ct);
        return member is not null && await db.RolePermissions.AnyAsync(p => p.Role == member.Role && p.Permission == permission, ct);
    }
    [HttpGet]
    public async Task<IActionResult> List(Guid tenantId, CancellationToken ct) => await Permitted(tenantId, "members.read", ct)
        ? Ok(await db.Memberships.AsNoTracking().Select(m => new { m.UserId, m.Role, m.IsActive }).ToListAsync(ct)) : Forbid();

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> Assign(Guid tenantId, Guid userId, MembershipRequest request, CancellationToken ct)
    {
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        if (request.UserId != userId || request.Role is not ("Staff" or "Customer" or "B2BBuyer")) return BadRequest(new { error = "Only non-administrative tenant roles may be assigned here." });
        var existing = await db.Memberships.SingleOrDefaultAsync(m => m.UserId == userId, ct);
        if (existing?.Role is "Owner" or "Manager") return Forbid();
        if (await users.FindByIdAsync(userId.ToString()) is null) return NotFound();
        if (existing is null) db.Memberships.Add(new TenantMembership { TenantId = tenantId, UserId = userId, Role = request.Role });
        else { existing.Role = request.Role; existing.IsActive = true; }
        await accounts.RevokeTenantSessionsAsync(userId, tenantId, ct);
        await accounts.AuditAsync(UserId, tenantId, "membership.assign", "success", ct);
        return NoContent();
    }
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Deactivate(Guid tenantId, Guid userId, CancellationToken ct)
    {
        if (!await Permitted(tenantId, "members.write", ct)) return Forbid();
        var existing = await db.Memberships.SingleOrDefaultAsync(m => m.UserId == userId, ct);
        if (existing is null) return NotFound();
        if (existing.Role is "Owner" or "Manager") return Forbid();
        existing.IsActive = false;
        await accounts.RevokeTenantSessionsAsync(userId, tenantId, ct);
        await accounts.AuditAsync(UserId, tenantId, "membership.deactivate", "success", ct);
        return NoContent();
    }
}
