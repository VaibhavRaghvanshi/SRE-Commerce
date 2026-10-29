using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;

public interface ITenantContext { Guid? TenantId { get; } }
public sealed class NoTenantContext : ITenantContext { public Guid? TenantId => null; }
public record RegistrationRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, StringLength(72, MinimumLength=12)] string Password, [Required, StringLength(160, MinimumLength=1)] string FullName, AccountKind Kind = AccountKind.Customer);
public record EmailRequest([Required, EmailAddress] string Email);
public record ConfirmEmailRequest([Required, EmailAddress] string Email, [Required] string Code);
public record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Code, [Required, StringLength(72, MinimumLength=12)] string Password);
public record ChangePasswordRequest([Required] string CurrentPassword, [Required, StringLength(72, MinimumLength=12)] string NewPassword);
public record ProfileRequest([Required, StringLength(160, MinimumLength=1)] string FullName);
public record MfaSetupRequest([Required] string Password);
public record MfaCodeRequest([Required] string Code);
public record MembershipRequest(Guid UserId, [Required] string Role);
public record ActivationRequest(bool IsActive);
public record AccountView(Guid Id, string Email, string FullName, AccountKind Kind, bool EmailConfirmed, bool MfaEnabled);
public record OperationResult(bool Succeeded, string[] Errors)
{
    public static OperationResult Success() => new(true, []);
    public static OperationResult Failure(params string[] errors) => new(false, errors);
}

public interface IIdentityAccounts
{
    Task<OperationResult> RegisterAsync(RegistrationRequest request, CancellationToken ct);
    Task RequestConfirmationAsync(string email, CancellationToken ct);
    Task<OperationResult> ConfirmEmailAsync(ConfirmEmailRequest request, CancellationToken ct);
    Task RequestResetAsync(string email, CancellationToken ct);
    Task<OperationResult> ResetAsync(ResetPasswordRequest request, CancellationToken ct);
    Task<AccountView?> GetAsync(Guid userId, CancellationToken ct);
    Task<OperationResult> UpdateProfileAsync(Guid userId, ProfileRequest request, CancellationToken ct);
    Task<OperationResult> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken ct);
    Task RevokeSessionsAsync(Guid userId, Guid? sessionId, CancellationToken ct);
    Task RevokeTenantSessionsAsync(Guid userId, Guid tenantId, CancellationToken ct);
    Task AuditAsync(Guid? userId, Guid? tenantId, string action, string outcome, CancellationToken ct);
}
