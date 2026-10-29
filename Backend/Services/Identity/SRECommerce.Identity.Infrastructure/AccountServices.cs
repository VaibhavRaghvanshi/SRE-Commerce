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

public sealed class LegacyPasswordHasher : PasswordHasher<ApplicationUser>
{
    public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (user.LegacyPasswordHash is not null && hashedPassword == "LEGACY_BCRYPT")
        {
            if (System.Text.Encoding.UTF8.GetByteCount(providedPassword)>72) return PasswordVerificationResult.Failed;
            try
            {
                if (BCrypt.Net.BCrypt.Verify(providedPassword, user.LegacyPasswordHash))
                {
                    user.LegacyPasswordHash = null;
                    return PasswordVerificationResult.SuccessRehashNeeded;
                }
            }
            catch (BCrypt.Net.SaltParseException) { }
            return PasswordVerificationResult.Failed;
        }
        return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}

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

public sealed class LocalEmailWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<LocalEmailWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("Mailpit:Enabled")) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            try
            {
                // One local dispatcher; stable Message-ID supports at-least-once delivery.
                var jobs = await db.Emails.Where(e => e.DeliveredAt == null && e.NextAttemptAt <= DateTimeOffset.UtcNow).OrderBy(e => e.CreatedAt).Take(10).ToListAsync(stoppingToken);
                var protector = scope.ServiceProvider.GetRequiredService<IDataProtectionProvider>().CreateProtector("SRECommerce.Identity.Email.v1");
                foreach (var job in jobs)
                {
                    try
                    {
                        var host=configuration["Mailpit:Host"] ?? "127.0.0.1";
                        if (host is not ("127.0.0.1" or "localhost" or "mailpit" or "host.docker.internal"))
                            throw new InvalidOperationException("Only the local Mailpit capture adapter is configured.");
                        using var smtp = new SmtpClient(host, configuration.GetValue("Mailpit:Port", 1025));
                        using var message = new MailMessage("identity@srecommerce.local", job.Recipient, job.Subject, protector.Unprotect(job.Body));
                        message.Headers.Add("Message-ID", $"<{job.Id:N}@identity.srecommerce.local>");
                        await smtp.SendMailAsync(message, stoppingToken);
                        job.DeliveredAt = DateTimeOffset.UtcNow;
                    }
                    catch (Exception ex) when (ex is SmtpException or System.Security.Cryptography.CryptographicException)
                    {
                        job.Attempts++;
                        job.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, 5 * Math.Pow(2, Math.Min(job.Attempts, 6))));
                        logger.LogWarning("Local email delivery deferred for job {JobId}; attempt {Attempt}", job.Id, job.Attempts);
                    }
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning("Local email dispatcher unavailable; durable jobs remain pending ({ErrorType})", ex.GetType().Name);
            }
        }
    }
}
