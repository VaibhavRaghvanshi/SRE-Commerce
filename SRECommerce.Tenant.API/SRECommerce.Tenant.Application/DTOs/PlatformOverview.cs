using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;

public sealed record TenantStatusCount(TenantStatus Status, int Count);
public sealed record PlatformCount(string Code, int Count);
public sealed record RegistrationDay(DateTime Date, int Count);
public sealed record PlatformOverview(DateTimeOffset SampledAt, int TotalTenants, int EffectiveSubscriptions,
    int ExpiringNextSevenDays, int ActivePlans, TenantStatusCount[] TenantStates, PlatformCount[] BusinessTypes,
    PlatformCount[] ConfiguredModules, RegistrationDay[] Registrations);
public sealed record PlatformServiceHealth(string Service, string Status, long? ElapsedMilliseconds);
public sealed record PlatformHealthSnapshot(DateTimeOffset SampledAt, PlatformServiceHealth[] Services);
