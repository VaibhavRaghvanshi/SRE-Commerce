using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;

public record TenantCaller(Guid UserId,string AccountKind,bool IsPlatformAdmin,Guid? SelectedTenantId,string? TenantRole);
public interface ITenantCaller { TenantCaller Current { get; } }
public sealed class TenantDataScope { public Guid? TenantId { get; set; } }
public sealed class IdentityUnavailableException : Exception;
public interface IIdentityOwnerProvisioner { Task ProvisionOwnerAsync(Guid tenantId,Guid userId,CancellationToken ct); }
public interface IDomainOwnershipVerifier { Task<IReadOnlyCollection<string>> GetTxtAsync(string host,CancellationToken ct); }
public record RegistrationRequest([Required,StringLength(160)]string Name,[Required,StringLength(63)]string Slug,[Required,StringLength(40)]string BusinessTypeCode,[StringLength(100)]string? CustomBusinessType);
public record VersionRequest([Required,MaxLength(24)]string Version);
public record SettingsRequest([Required,MaxLength(24)]string Version,[Required,StringLength(3,MinimumLength=3)]string Currency,[Required,MaxLength(20)]string Culture,[Required,MaxLength(100)]string TimeZone,[Required,MaxLength(160)]string DisplayName,[MaxLength(500)]string? LogoUrl);
public record ModuleRequest([Required,MaxLength(24)]string Version,bool Enabled);
public record SubscriptionRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(40)]string PlanCode,DateTimeOffset StartsAt,DateTimeOffset EndsAt,SubscriptionStatus Status);
public record SuspensionRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(300)]string Reason);
public record LocationRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(160)]string Name,[Required,MaxLength(500)]string Address,[Required,StringLength(2,MinimumLength=2)]string CountryCode);
public record DomainRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(253)]string Host);
public record DomainVerificationRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(100)]string Token,bool MakePrimary=false);
public record PlanRequest([Required,MaxLength(40)]string Code,[Required,MaxLength(100)]string Name,[Range(1,100)]int MaxLocations,[Range(0,50)]int MaxDomains,[Range(1,100000)]int MaxUsers,[Required,MaxLength(12)]string[] Modules);
public record TenantView(Guid Id,Guid OwnerUserId,string Name,string Slug,string BusinessTypeCode,string? CustomBusinessType,TenantStatus Status,string? SuspensionReason,DatabaseStrategy DatabaseStrategy,string Version,DateTimeOffset CreatedAt);
public record SettingsView(string Currency,string Culture,string TimeZone,string DisplayName,string? LogoUrl);
public record ModuleView(string Module,bool Configured,bool Entitled,bool Enabled);
public record EntitlementView(Guid TenantId,bool Active,string PlanCode,DateTimeOffset EndsAt,int MaxLocations,int MaxDomains,int MaxUsers,IReadOnlyCollection<ModuleView> Modules);
public record DomainView(Guid Id,string Host,DateTimeOffset? VerifiedAt,bool IsPrimary,bool IsActive);
public record DomainChallenge(Guid Id,string Host,string RecordName,string Token,DateTimeOffset ExpiresAt,string Version);
public record LocationView(Guid Id,string Name,string Address,string CountryCode,bool IsActive);
public interface ITenantOperations
{
    Task<IReadOnlyCollection<BusinessType>> BusinessTypes(CancellationToken ct);
    Task<object> Plans(CancellationToken ct);
    Task CreatePlan(PlanRequest request,CancellationToken ct);
    Task<IReadOnlyCollection<TenantView>> List(int page,CancellationToken ct);
    Task<TenantView> Register(Guid requestId,RegistrationRequest request,CancellationToken ct);
    Task<TenantView> Get(Guid id,CancellationToken ct);
    Task<TenantView> Submit(Guid id,VersionRequest request,CancellationToken ct);
    Task<TenantView> Activate(Guid id,VersionRequest request,CancellationToken ct);
    Task<TenantView> Suspend(Guid id,SuspensionRequest request,CancellationToken ct);
    Task<TenantView> Archive(Guid id,VersionRequest request,CancellationToken ct);
    Task<SettingsView> Settings(Guid id,CancellationToken ct);
    Task<TenantView> UpdateSettings(Guid id,SettingsRequest request,CancellationToken ct);
    Task<EntitlementView> Entitlements(Guid id,CancellationToken ct);
    Task<EntitlementView> MemberEntitlements(Guid id,CancellationToken ct);
    Task<TenantView> SetModule(Guid id,string module,ModuleRequest request,CancellationToken ct);
    Task<TenantView> SetSubscription(Guid id,SubscriptionRequest request,CancellationToken ct);
    Task<IReadOnlyCollection<LocationView>> Locations(Guid id,CancellationToken ct);
    Task<TenantView> AddLocation(Guid id,LocationRequest request,CancellationToken ct);
    Task<TenantView> DeactivateLocation(Guid id,Guid locationId,VersionRequest request,CancellationToken ct);
    Task<IReadOnlyCollection<DomainView>> Domains(Guid id,CancellationToken ct);
    Task<DomainChallenge> AddDomain(Guid id,DomainRequest request,CancellationToken ct);
    Task<TenantView> VerifyDomain(Guid id,Guid domainId,DomainVerificationRequest request,CancellationToken ct);
    Task<TenantView> DeactivateDomain(Guid id,Guid domainId,VersionRequest request,CancellationToken ct);
    Task<object> Audit(Guid id,int page,CancellationToken ct);
    Task<object> PlatformHistory(int page,CancellationToken ct);
}
