using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SRECommerce.Identity.Application;

namespace SRECommerce.Identity.API;

[ApiController, Authorize(Policy = "ActiveSession")]
[Route("api/v1/me/stores/{tenantId:guid}/membership")]
public sealed class CustomerEnrollmentController(ICustomerEnrollment enrollment) : ControllerBase
{
    [HttpPost, EnableRateLimiting("auth")]
    public async Task<IActionResult> Join(Guid tenantId, CancellationToken ct)
    {
        var result = await enrollment.JoinAsync(Guid.Parse(User.FindFirstValue("sub")!), tenantId, ct);
        return result.Status switch
        {
            EnrollmentStatus.Joined or EnrollmentStatus.AlreadyMember => Ok(new { tenantId, role = result.Role, joined = result.Status == EnrollmentStatus.Joined }),
            EnrollmentStatus.Blocked => StatusCode(403, new { error = "MembershipUnavailable" }),
            EnrollmentStatus.StoreNotFound => NotFound(new { error = "StoreNotFound" }),
            _ => StatusCode(503, new { error = "StoreDirectoryUnavailable" })
        };
    }
}
