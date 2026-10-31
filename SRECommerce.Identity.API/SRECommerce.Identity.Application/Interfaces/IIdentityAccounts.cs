using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;

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
