using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;
using SRECommerce.Identity.Infrastructure;

namespace SRECommerce.Identity.API;

[ApiController]
[Route("api/v1")]
public sealed class AccountsController(IIdentityAccounts accounts, IdentityDbContext db, UserManager<ApplicationUser> users) : ControllerBase
{
    private Guid UserId => Guid.Parse(User.FindFirstValue("sub")!);
    private IActionResult Outcome(OperationResult result) => result.Succeeded ? NoContent() : BadRequest(new { errors = result.Errors });
    private IActionResult AcceptedMessage() => Accepted(new { message = "If the account is eligible, instructions will be delivered." });

    [HttpPost("accounts/register"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegistrationRequest request, CancellationToken ct)
    {
        var result = await accounts.RegisterAsync(request, ct);
        return result.Succeeded ? AcceptedMessage() : BadRequest(new { errors = result.Errors });
    }
    [HttpPost("accounts/confirmation"), EnableRateLimiting("auth")]
    public async Task<IActionResult> RequestConfirmation(EmailRequest request, CancellationToken ct) { await accounts.RequestConfirmationAsync(request.Email, ct); return AcceptedMessage(); }
    [HttpPost("accounts/confirm-email"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Confirm(ConfirmEmailRequest request, CancellationToken ct) => Outcome(await accounts.ConfirmEmailAsync(request, ct));
    [HttpPost("accounts/forgot-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Forgot(EmailRequest request, CancellationToken ct) { await accounts.RequestResetAsync(request.Email, ct); return AcceptedMessage(); }
    [HttpPost("accounts/reset-password"), EnableRateLimiting("auth")]
    public async Task<IActionResult> Reset(ResetPasswordRequest request, CancellationToken ct) => Outcome(await accounts.ResetAsync(request, ct));
    [HttpGet("me"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(await accounts.GetAsync(UserId, ct));
    [HttpPut("me"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> UpdateProfile(ProfileRequest request, CancellationToken ct) => Outcome(await accounts.UpdateProfileAsync(UserId, request, ct));
    [HttpPost("me/password"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken ct) => Outcome(await accounts.ChangePasswordAsync(UserId, request, ct));
    [HttpGet("me/sessions"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> Sessions(CancellationToken ct) => Ok(await db.Sessions.AsNoTracking().Where(s => s.UserId == UserId).OrderByDescending(s => s.CreatedAt).Select(s => new { s.Id, s.TenantId, s.CreatedAt, s.ExpiresAt, s.RevokedAt, s.MfaAuthenticated }).ToListAsync(ct));
    [HttpDelete("me/sessions/{id:guid}"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> Revoke(Guid id, CancellationToken ct)
    {
        if (!await db.Sessions.AnyAsync(s => s.UserId == UserId && s.Id == id, ct)) return NotFound();
        await accounts.RevokeSessionsAsync(UserId, id, ct);
        await accounts.AuditAsync(UserId, null, "session.revoke", "success", ct);
        return NoContent();
    }
    [HttpPost("logout"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await accounts.RevokeSessionsAsync(UserId, Guid.Parse(User.FindFirstValue("sid")!), ct);
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        await accounts.AuditAsync(UserId, null, "logout", "success", ct);
        return NoContent();
    }
    [HttpGet("me/memberships"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> MyMemberships(CancellationToken ct) => Ok(await db.Memberships.IgnoreQueryFilters().AsNoTracking().Where(m => m.UserId == UserId).Select(m => new { m.TenantId, m.Role, m.IsActive }).ToListAsync(ct));

    [HttpPost("me/mfa/setup"), Authorize(Policy="ActiveSession"), EnableRateLimiting("auth")]
    public async Task<IActionResult> SetupMfa(MfaSetupRequest request, CancellationToken ct)
    {
        var user = (await users.FindByIdAsync(UserId.ToString()))!;
        if (user.TwoFactorEnabled || !await users.CheckPasswordAsync(user, request.Password)) return BadRequest(new { error = "MFA setup is not available." });
        await users.ResetAuthenticatorKeyAsync(user);
        // ResetAuthenticatorKey changes Identity's security stamp. Preserve only
        // this password-verified enrollment session; revoke all other sessions.
        var currentId = Guid.Parse(User.FindFirstValue("sid")!);
        var current = await db.Sessions.SingleAsync(s => s.Id == currentId && s.UserId == UserId, ct);
        current.SecurityStamp = user.SecurityStamp!;
        var otherSessions = await db.Sessions.Where(s => s.UserId == UserId && s.Id != currentId && s.RevokedAt == null).Select(s => s.Id).ToListAsync(ct);
        foreach (var otherId in otherSessions) await accounts.RevokeSessionsAsync(UserId, otherId, ct);
        var key = await users.GetAuthenticatorKeyAsync(user);
        await accounts.AuditAsync(UserId, null, "mfa.setup", "pending-confirmation", ct);
        return Ok(new { sharedKey = key, authenticatorUri = $"otpauth://totp/SRECommerce:{Uri.EscapeDataString(user.Email!)}?secret={key}&issuer=SRECommerce&digits=6" });
    }
    [HttpPost("me/mfa/enable"), Authorize(Policy="ActiveSession"), EnableRateLimiting("auth")]
    public async Task<IActionResult> EnableMfa(MfaCodeRequest request, CancellationToken ct)
    {
        var user = (await users.FindByIdAsync(UserId.ToString()))!;
        if (user.TwoFactorEnabled || !await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, request.Code.Replace(" ", ""))) return BadRequest(new { error = "Invalid authenticator code." });
        await users.SetTwoFactorEnabledAsync(user, true);
        var recoveryCodes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 8);
        await users.UpdateSecurityStampAsync(user);
        await accounts.RevokeSessionsAsync(UserId, null, ct);
        await accounts.AuditAsync(UserId, null, "mfa.enable", "success-relogin-required", ct);
        return Ok(new { recoveryCodes, message = "MFA enabled. Sign in again." });
    }
    [HttpPost("me/mfa/disable"), Authorize(Policy="ActiveSession"), EnableRateLimiting("auth")]
    public async Task<IActionResult> DisableMfa(MfaCodeRequest request, CancellationToken ct)
    {
        var user = (await users.FindByIdAsync(UserId.ToString()))!;
        if (!user.TwoFactorEnabled || !await users.VerifyTwoFactorTokenAsync(user, users.Options.Tokens.AuthenticatorTokenProvider, request.Code.Replace(" ", ""))) return BadRequest(new { error = "Invalid authenticator code." });
        await users.SetTwoFactorEnabledAsync(user, false);
        await users.ResetAuthenticatorKeyAsync(user);
        await users.UpdateSecurityStampAsync(user);
        await accounts.RevokeSessionsAsync(UserId, null, ct);
        await accounts.AuditAsync(UserId, null, "mfa.disable", "success-relogin-required", ct);
        return NoContent();
    }

    [HttpGet("me/security-history"), Authorize(Policy="ActiveSession")]
    public async Task<IActionResult> SecurityHistory([FromQuery] int page=1, CancellationToken ct=default)
    {
        if (page < 1 || page > 10000) return BadRequest();
        return Ok(await db.AuditLogs.AsNoTracking().Where(a=>a.UserId==UserId).OrderByDescending(a=>a.Id).Skip((page-1)*50).Take(50).ToListAsync(ct));
    }
}
