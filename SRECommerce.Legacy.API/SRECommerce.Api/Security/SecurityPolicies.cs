using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Infrastructure.Data;

namespace SRECommerce.Api.Security;

public static class SecurityPolicies
{
    public const string ManageUsers = "ManageUsers";
}

public sealed class ManageUsersRequirement : IAuthorizationRequirement;

// Recheck storage so demoted/deleted admins cannot manage accounts with an old JWT.
public sealed class ManageUsersHandler(AppDbContext db) : AuthorizationHandler<ManageUsersRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ManageUsersRequirement requirement)
    {
        var subject = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated == true && context.User.IsInRole(Roles.Admin)
            && int.TryParse(subject, out var userId)
            && await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId && u.Role == Roles.Admin))
            context.Succeed(requirement);
    }
}

public static class AdminUploadLimits
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const long MaxRequestBytes = MaxImageBytes + 64 * 1024;
}
