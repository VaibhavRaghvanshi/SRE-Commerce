using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
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
