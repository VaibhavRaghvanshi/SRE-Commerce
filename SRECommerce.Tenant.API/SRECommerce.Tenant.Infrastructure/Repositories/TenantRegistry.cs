using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;
public sealed class TenantRegistry(TenantDbContext db):ITenantRegistry
{
    public async Task<PublicTenant?> PublicById(Guid id,CancellationToken ct)=>await Public(await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t=>t.Id==id&&t.Status==TenantStatus.Active,ct),ct);
    public async Task<PublicTenant?> PublicBySlug(string slug,CancellationToken ct)
    {var key=TenantRules.Slug(slug);return await Public(await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t=>t.Slug==key&&t.Status==TenantStatus.Active,ct),ct);}
    private async Task<PublicTenant?> Public(BusinessTenant? tenant,CancellationToken ct)
    {
        if(tenant is null)return null;var subscription=await db.Subscriptions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(s=>s.TenantId==tenant.Id,ct);
        if(subscription is null||!subscription.IsEffective(DateTimeOffset.UtcNow)||!await db.Plans.AnyAsync(p=>p.Code==subscription.PlanCode&&p.IsActive,ct))return null;
        var settings=await db.Settings.IgnoreQueryFilters().AsNoTracking().SingleAsync(s=>s.TenantId==tenant.Id,ct);
        var permitted=await db.Features.Where(f=>f.PlanCode==subscription.PlanCode&&f.Enabled).Select(f=>f.Feature).ToListAsync(ct);
        var modules=await db.Modules.IgnoreQueryFilters().Where(m=>m.TenantId==tenant.Id&&m.Enabled&&permitted.Contains(m.Module)).Select(m=>m.Module).ToArrayAsync(ct);
        return new(tenant.Id,tenant.Slug,tenant.BusinessTypeCode,settings.DisplayName,settings.LogoUrl,settings.Currency,modules);
    }
    public async Task<ProvisioningLimits?> Limits(Guid id,CancellationToken ct)
    {
        var tenant=await db.Tenants.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(t=>t.Id==id,ct);if(tenant is null)return null;
        var subscription=await db.Subscriptions.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(s=>s.TenantId==id,ct);if(subscription is null)return null;
        var plan=await db.Plans.AsNoTracking().SingleAsync(p=>p.Code==subscription.PlanCode,ct);
        return new(id,tenant.Status!=TenantStatus.Archived&&plan.IsActive&&subscription.IsEffective(DateTimeOffset.UtcNow),plan.MaxUsers);
    }
}
