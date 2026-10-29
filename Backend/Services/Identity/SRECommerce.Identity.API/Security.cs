using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Infrastructure;
using OpenIddict.Abstractions;

namespace SRECommerce.Identity.API;

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid? TenantId => Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("tenant_id"), out var id) ? id : null;
}
public sealed class ActiveSessionRequirement : IAuthorizationRequirement;
public sealed class ActiveSessionHandler(IdentityDbContext db) : AuthorizationHandler<ActiveSessionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ActiveSessionRequirement requirement)
    {
        if (!context.User.HasScope("identity_api")) return;
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out var userId)
            || !Guid.TryParse(context.User.FindFirstValue("sid"), out var sessionId)) return;
        var session = await db.Sessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId && s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTimeOffset.UtcNow);
        if (session is null || !await db.Profiles.AnyAsync(p => p.UserId == userId && p.IsActive)) return;
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null || user.SecurityStamp != session.SecurityStamp || !user.EmailConfirmed || (user.TwoFactorEnabled && !session.MfaAuthenticated)) return;
        if (session.TenantId is { } tenantId && !await db.Memberships.IgnoreQueryFilters().AnyAsync(m => m.UserId == userId && m.TenantId == tenantId && m.IsActive)) return;
        context.Succeed(requirement);
    }
}
