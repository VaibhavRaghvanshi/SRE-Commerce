using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Infrastructure;

public sealed class IdentityAccounts(IdentityDbContext db, UserManager<ApplicationUser> users,
    IOpenIddictAuthorizationManager authorizations, IOpenIddictTokenManager tokens, IDataProtectionProvider protection) : IIdentityAccounts
{
    private readonly IDataProtector emailProtector = protection.CreateProtector("SRECommerce.Identity.Email.v1");
    private static OperationResult Result(IdentityResult result) => result.Succeeded ? OperationResult.Success() : OperationResult.Failure(result.Errors.Select(e => e.Code).ToArray());

    public async Task<OperationResult> RegisterAsync(RegistrationRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) return OperationResult.Failure("InvalidFullName");
        if (request.Kind is not (AccountKind.Customer or AccountKind.BusinessOwner or AccountKind.B2BBuyer))
            return OperationResult.Failure("InvalidAccountKind");
        var email = request.Email.Trim();
        // Use the same neutral response for duplicates; no role or tenant can be self-selected.
        if (await users.FindByEmailAsync(email) is not null) return OperationResult.Success();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, LockoutEnabled = true };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded) return Result(result);
        var profile = new UserProfile { UserId = user.Id, Kind = request.Kind };
        profile.Rename(request.FullName);
        db.Profiles.Add(profile);
        QueueEmail(user, "Confirm your SRECommerce account", await users.GenerateEmailConfirmationTokenAsync(user), "confirm-email");
        db.AuditLogs.Add(new SecurityAuditLog { UserId = user.Id, Action = "account.register", Outcome = "success" });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return OperationResult.Success();
    }

    public async Task RequestConfirmationAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || user.EmailConfirmed || !await ActiveAsync(user.Id, ct)) return;
        QueueEmail(user, "Confirm your SRECommerce account", await users.GenerateEmailConfirmationTokenAsync(user), "confirm-email");
        await db.SaveChangesAsync(ct);
    }

    public async Task<OperationResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await ActiveAsync(user.Id, ct)) return OperationResult.Failure("InvalidConfirmation");
        var result = await users.ConfirmEmailAsync(user, request.Code);
        await AuditAsync(user.Id, null, "email.confirm", result.Succeeded ? "success" : "failure", ct);
        return result.Succeeded ? OperationResult.Success() : OperationResult.Failure("InvalidConfirmation");
    }

    public async Task RequestResetAsync(string email, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.EmailConfirmed || !await ActiveAsync(user.Id, ct)) return;
        QueueEmail(user, "Reset your SRECommerce password", await users.GeneratePasswordResetTokenAsync(user), "reset-password");
        await db.SaveChangesAsync(ct);
    }

    public async Task<OperationResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.EmailConfirmed || !await ActiveAsync(user.Id, ct)) return OperationResult.Failure("InvalidReset");
        var result = await users.ResetPasswordAsync(user, request.Code, request.Password);
        if (result.Succeeded)
        {
            user.LegacyPasswordHash = null;
            await users.UpdateAsync(user);
            await RevokeSessionsAsync(user.Id, null, ct);
        }
        await AuditAsync(user.Id, null, "password.reset", result.Succeeded ? "success" : "failure", ct);
        return result.Succeeded ? OperationResult.Success() : OperationResult.Failure("InvalidReset");
    }

    public async Task<AccountView?> GetAsync(Guid userId, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        var profile = await db.Profiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId && p.IsActive, ct);
        return user is null || profile is null ? null : new(user.Id, user.Email!, profile.FullName, profile.Kind, user.EmailConfirmed, user.TwoFactorEnabled);
    }

    public async Task<OperationResult> UpdateProfileAsync(Guid userId, ProfileRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)) return OperationResult.Failure("InvalidFullName");
        var profile = await db.Profiles.SingleAsync(p => p.UserId == userId, ct);
        profile.Rename(request.FullName);
        await AuditAsync(userId, null, "profile.update", "success", ct);
        return OperationResult.Success();
    }

    public async Task<OperationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return OperationResult.Failure("InvalidAccount");
        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (result.Succeeded) await RevokeSessionsAsync(userId, null, ct);
        await AuditAsync(userId, null, "password.change", result.Succeeded ? "success" : "failure", ct);
        return Result(result);
    }

    public async Task RevokeSessionsAsync(Guid userId, Guid? sessionId, CancellationToken ct)
    {
        var sessions = await db.Sessions.Where(s => s.UserId == userId && (sessionId == null || s.Id == sessionId) && s.RevokedAt == null).ToListAsync(ct);
        await RevokeAsync(sessions, ct);
    }

    public async Task RevokeTenantSessionsAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        var sessions = await db.Sessions.Where(s => s.UserId == userId && s.TenantId == tenantId && s.RevokedAt == null).ToListAsync(ct);
        await RevokeAsync(sessions, ct);
    }

    private async Task RevokeAsync(List<UserSession> sessions, CancellationToken ct)
    {
        foreach (var session in sessions)
        {
            session.RevokedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(session.AuthorizationId))
            {
                var authorization = await authorizations.FindByIdAsync(session.AuthorizationId, ct);
                if (authorization is not null) await authorizations.TryRevokeAsync(authorization, ct);
                await tokens.RevokeByAuthorizationIdAsync(session.AuthorizationId, ct);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task AuditAsync(Guid? userId, Guid? tenantId, string action, string outcome, CancellationToken ct)
    {
        db.AuditLogs.Add(new SecurityAuditLog { UserId = userId, TenantId = tenantId, Action = action, Outcome = outcome });
        await db.SaveChangesAsync(ct);
    }

    private Task<bool> ActiveAsync(Guid id, CancellationToken ct) => db.Profiles.AnyAsync(p => p.UserId == id && p.IsActive, ct);
    private void QueueEmail(ApplicationUser user, string subject, string code, string purpose) => db.Emails.Add(new EmailOutbox
    {
        UserId = user.Id, Recipient = user.Email!, Subject = subject,
        Body = emailProtector.Protect(JsonSerializer.Serialize(new { email = user.Email, code, purpose }))
    });
}
