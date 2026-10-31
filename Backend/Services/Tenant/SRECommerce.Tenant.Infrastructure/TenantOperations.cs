using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;

public sealed class TenantOperations(TenantDbContext db,ITenantCaller accessor,TenantDataScope dataScope,IIdentityOwnerProvisioner identity,IDomainOwnershipVerifier dns) : ITenantOperations
{
    private TenantCaller Caller => accessor.Current;
    private static TenantView View(BusinessTenant t)=>new(t.Id,t.OwnerUserId,t.Name,t.Slug,t.BusinessTypeCode,t.CustomBusinessType,t.Status,t.SuspensionReason,t.DatabaseStrategy,Convert.ToBase64String(t.Version),t.CreatedAt);
    private void Admin() { if(!Caller.IsPlatformAdmin) throw new TenantRuleException("PlatformAdministratorRequired",403); }
    private static void Page(int page) { if(page is <1 or >10000) throw new TenantRuleException("InvalidPage",400); }
    private async Task<BusinessTenant> Load(Guid id,bool manage,CancellationToken ct,bool allowMember=false)
    {
        var tenant=await db.Tenants.SingleOrDefaultAsync(t=>t.Id==id,ct) ?? throw new TenantRuleException("TenantNotFound",404);
        var onboarding=tenant.OwnerUserId==Caller.UserId && tenant.Status is TenantStatus.Draft or TenantStatus.PendingApproval;
        var selected=Caller.SelectedTenantId==id && (Caller.TenantRole is "Owner" or "Manager" or "Staff" || allowMember && Caller.TenantRole is "Customer" or "B2BBuyer");
        if(!Caller.IsPlatformAdmin && !onboarding && !selected) throw new TenantRuleException("TenantAccessDenied",403);
        if(manage && !Caller.IsPlatformAdmin && !onboarding && Caller.TenantRole is not ("Owner" or "Manager")) throw new TenantRuleException("TenantManagementRequired",403);
        dataScope.TenantId=id;
        if(Caller.IsPlatformAdmin && !manage)
        {
            db.Audit.Add(new TenantAudit{TenantId=id,ActorUserId=Caller.UserId,Action="platform.tenant.read",PlatformOperation=true});
            await db.SaveChangesAsync(ct);
        }
        return tenant;
    }
    private static void Version(BusinessTenant tenant,string value)
    {
        byte[] expected;
        try { expected=Convert.FromBase64String(value); } catch(FormatException) { throw new TenantRuleException("InvalidVersion",400); }
        if(expected.Length!=8) throw new TenantRuleException("InvalidVersion",400);
        if(!expected.SequenceEqual(tenant.Version)) throw new TenantRuleException("VersionConflict");
    }
    private void Record(BusinessTenant tenant,string action)
    {
        db.Audit.Add(new TenantAudit{TenantId=tenant.Id,ActorUserId=Caller.UserId,Action=action,PlatformOperation=Caller.IsPlatformAdmin});
        db.Outbox.Add(new TenantOutbox{TenantId=tenant.Id,Type=action,Payload=JsonSerializer.Serialize(new{tenantId=tenant.Id,status=tenant.Status.ToString(),schemaVersion=1})});
    }
    private async Task<TenantView> Mutate(Guid id,string version,string action,Func<BusinessTenant,Task> change,CancellationToken ct)
    {
        await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        var tenant=await Load(id,true,ct); Version(tenant,version); tenant.EnsureEditable();
        await change(tenant); tenant.UpdatedAt=DateTimeOffset.UtcNow; Record(tenant,action);
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return View(tenant);
    }
    public async Task<IReadOnlyCollection<BusinessType>> BusinessTypes(CancellationToken ct)=>await db.BusinessTypes.AsNoTracking().Where(t=>t.IsActive).OrderBy(t=>t.Code).ToListAsync(ct);
    public async Task<object> Plans(CancellationToken ct)=>await db.Plans.AsNoTracking().Where(p=>p.IsActive).OrderBy(p=>p.Code).Select(p=>new{p.Code,p.Name,p.MaxLocations,p.MaxDomains,p.MaxUsers,Modules=db.Features.Where(f=>f.PlanCode==p.Code && f.Enabled).Select(f=>f.Feature).ToList()}).ToListAsync(ct);
    public async Task CreatePlan(PlanRequest request,CancellationToken ct)
    {
        Admin(); var code=TenantRules.Slug(request.Code);
        if(request.Modules.Distinct(StringComparer.Ordinal).Count()!=request.Modules.Length || request.Modules.Any(m=>!TenantRules.Modules.Contains(m))) throw new TenantRuleException("InvalidPlanModules",400);
        if(await db.Plans.AnyAsync(p=>p.Code==code,ct)) throw new TenantRuleException("PlanAlreadyExists");
        await using var transaction=await db.Database.BeginTransactionAsync(ct);
        db.Plans.Add(new SubscriptionPlan{Code=code,Name=TenantRules.Name(request.Name,100),MaxDomains=request.MaxDomains,MaxLocations=request.MaxLocations,MaxUsers=request.MaxUsers});
        foreach(var module in request.Modules) db.Features.Add(new PlanFeature{PlanCode=code,Feature=module,Enabled=true});
        db.PlatformAudit.Add(new TenantPlatformAudit{ActorUserId=Caller.UserId,Action="plan.created",Resource=code});
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
    }
    public async Task<IReadOnlyCollection<TenantView>> List(int page,CancellationToken ct)
    {
        Page(page);
        var tenants=await db.Tenants.AsNoTracking().OrderBy(t=>t.CreatedAt).ThenBy(t=>t.Id).Skip((page-1)*50).Take(50).ToListAsync(ct);
        if(!Caller.IsPlatformAdmin && Caller.TenantRole is not ("Owner" or "Manager" or "Staff")) tenants=tenants.Where(t=>t.OwnerUserId==Caller.UserId && t.Status is TenantStatus.Draft or TenantStatus.PendingApproval).ToList();
        if(Caller.IsPlatformAdmin)
        {
            db.PlatformAudit.Add(new TenantPlatformAudit{ActorUserId=Caller.UserId,Action="tenants.list",Resource="page:"+page});
            foreach(var tenant in tenants) db.Audit.Add(new TenantAudit{TenantId=tenant.Id,ActorUserId=Caller.UserId,Action="platform.tenants.list",PlatformOperation=true});
            await db.SaveChangesAsync(ct);
        }
        return tenants.Select(View).ToList();
    }
    public async Task<TenantView> Register(Guid requestId,RegistrationRequest request,CancellationToken ct)
    {
        if(Caller.AccountKind!="BusinessOwner" || requestId==Guid.Empty) throw new TenantRuleException("BusinessOwnerAndRequestIdRequired",403);
        var name=TenantRules.Name(request.Name);var slug=TenantRules.Slug(request.Slug);var type=request.BusinessTypeCode.Trim().ToLowerInvariant();
        var custom=type=="custom" ? TenantRules.Name(request.CustomBusinessType??"",100) : null;
        if(type!="custom" && !string.IsNullOrWhiteSpace(request.CustomBusinessType)) throw new TenantRuleException("UnexpectedCustomBusinessType",400);
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{name,slug,type,custom}))));
        var prior=await db.Tenants.IgnoreQueryFilters().SingleOrDefaultAsync(t=>t.OwnerUserId==Caller.UserId && t.RegistrationRequestId==requestId,ct);
        if(prior is not null) { if(prior.RegistrationFingerprint!=fingerprint) throw new TenantRuleException("RegistrationKeyReused");return View(prior); }
        await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        if(!await db.BusinessTypes.AnyAsync(t=>t.Code==type && t.IsActive,ct)) throw new TenantRuleException("UnknownBusinessType",400);
        if(await db.Tenants.IgnoreQueryFilters().AnyAsync(t=>t.Slug==slug,ct)) throw new TenantRuleException("SlugUnavailable");
        if(!await db.Plans.AnyAsync(p=>p.Code=="starter" && p.IsActive,ct)) throw new TenantRuleException("PlanCatalogUnavailable",503);
        var tenant=new BusinessTenant{OwnerUserId=Caller.UserId,RegistrationRequestId=requestId,RegistrationFingerprint=fingerprint,Name=name,Slug=slug,BusinessTypeCode=type,CustomBusinessType=custom};
        dataScope.TenantId=tenant.Id; db.Tenants.Add(tenant);db.Settings.Add(new TenantSettings{TenantId=tenant.Id,DisplayName=name});
        db.Subscriptions.Add(new TenantSubscription{TenantId=tenant.Id,PlanCode="starter",StartsAt=DateTimeOffset.UtcNow,EndsAt=DateTimeOffset.UtcNow.AddDays(30)});
        foreach(var module in TenantRules.Modules) db.Modules.Add(new TenantModule{TenantId=tenant.Id,Module=module,Enabled=module is "catalog" or "storefront"});
        Record(tenant,"tenant.registered");await db.SaveChangesAsync(ct);await transaction.CommitAsync(ct);return View(tenant);
    }
    public async Task<TenantView> Get(Guid id,CancellationToken ct)=>View(await Load(id,false,ct));
    public Task<TenantView> Submit(Guid id,VersionRequest request,CancellationToken ct)=>Mutate(id,request.Version,"tenant.submitted",t=>{t.Submit();return Task.CompletedTask;},ct);
    public async Task<TenantView> Activate(Guid id,VersionRequest request,CancellationToken ct)
    {
        Admin();var tenant=await Load(id,true,ct);Version(tenant,request.Version);
        if(tenant.Status is not (TenantStatus.PendingApproval or TenantStatus.Suspended)) throw new TenantRuleException("CannotActivate");
        var subscription=await db.Subscriptions.SingleAsync(s=>s.TenantId==id,ct);
        if(!subscription.IsEffective(DateTimeOffset.UtcNow)) throw new TenantRuleException("SubscriptionNotEffective");
        await identity.ProvisionOwnerAsync(id,tenant.OwnerUserId,ct);
        return await Mutate(id,request.Version,"tenant.activated",t=>{t.Activate();return Task.CompletedTask;},ct);
    }
    public Task<TenantView> Suspend(Guid id,SuspensionRequest request,CancellationToken ct)
    { Admin();return Mutate(id,request.Version,"tenant.suspended",t=>{t.Suspend(request.Reason);return Task.CompletedTask;},ct); }
    public Task<TenantView> Archive(Guid id,VersionRequest request,CancellationToken ct)
    { Admin();return Mutate(id,request.Version,"tenant.archived",t=>{if(t.Status!=TenantStatus.Suspended) throw new TenantRuleException("SuspendBeforeArchive");t.Status=TenantStatus.Archived;return Task.CompletedTask;},ct); }
    public async Task<SettingsView> Settings(Guid id,CancellationToken ct)
    {
        await Load(id,false,ct);var settings=await db.Settings.AsNoTracking().SingleAsync(s=>s.TenantId==id,ct);
        return new(settings.Currency,settings.Culture,settings.TimeZone,settings.DisplayName,settings.LogoUrl);
    }
    public Task<TenantView> UpdateSettings(Guid id,SettingsRequest request,CancellationToken ct)=>Mutate(id,request.Version,"tenant.settings.updated",async _=>
    {var settings=await db.Settings.SingleAsync(s=>s.TenantId==id,ct);settings.Configure(request.Currency,request.Culture,request.TimeZone,request.DisplayName,request.LogoUrl);},ct);
    private async Task<(TenantSubscription Subscription,SubscriptionPlan Plan,HashSet<string> Allowed)> Plan(Guid id,CancellationToken ct)
    {
        var subscription=await db.Subscriptions.AsNoTracking().SingleAsync(s=>s.TenantId==id,ct);
        var plan=await db.Plans.AsNoTracking().SingleAsync(p=>p.Code==subscription.PlanCode,ct);
        return(subscription,plan,(await db.Features.Where(f=>f.PlanCode==plan.Code && f.Enabled).Select(f=>f.Feature).ToListAsync(ct)).ToHashSet(StringComparer.Ordinal));
    }
    public async Task<EntitlementView> Entitlements(Guid id,CancellationToken ct)
    {
        var tenant=await Load(id,false,ct);return await Effective(tenant,ct);
    }
    public async Task<EntitlementView> MemberEntitlements(Guid id,CancellationToken ct)
    {
        var tenant=await Load(id,false,ct,true);return await Effective(tenant,ct);
    }
    private async Task<EntitlementView> Effective(BusinessTenant tenant,CancellationToken ct)
    {
        var id=tenant.Id;var (subscription,plan,allowed)=await Plan(id,ct);
        var active=tenant.Status==TenantStatus.Active && subscription.IsEffective(DateTimeOffset.UtcNow) && plan.IsActive;
        var configured=await db.Modules.AsNoTracking().Where(m=>m.TenantId==id).ToListAsync(ct);
        return new(id,active,plan.Code,subscription.EndsAt,plan.MaxLocations,plan.MaxDomains,plan.MaxUsers,configured.Select(m=>new ModuleView(m.Module,m.Enabled,allowed.Contains(m.Module),active && m.Enabled && allowed.Contains(m.Module))).ToList());
    }
    public Task<TenantView> SetModule(Guid id,string module,ModuleRequest request,CancellationToken ct)=>Mutate(id,request.Version,"tenant.module.updated",async _=>
    {
        if(!TenantRules.Modules.Contains(module)) throw new TenantRuleException("UnknownModule",400);
        var (_,_,allowed)=await Plan(id,ct);if(request.Enabled && !allowed.Contains(module)) throw new TenantRuleException("ModuleNotEntitled");
        var item=await db.Modules.SingleAsync(m=>m.TenantId==id && m.Module==module,ct);item.Enabled=request.Enabled;
    },ct);
    public Task<TenantView> SetSubscription(Guid id,SubscriptionRequest request,CancellationToken ct)
    {
        Admin();return Mutate(id,request.Version,"tenant.subscription.updated",async _=>
        {
            if(!Enum.IsDefined(request.Status) || request.StartsAt>=request.EndsAt || request.EndsAt>DateTimeOffset.UtcNow.AddYears(5)) throw new TenantRuleException("InvalidSubscription",400);
            var plan=await db.Plans.SingleOrDefaultAsync(p=>p.Code==request.PlanCode && p.IsActive,ct) ?? throw new TenantRuleException("UnknownPlan",400);
            if(await db.Locations.CountAsync(l=>l.TenantId==id && l.IsActive,ct)>plan.MaxLocations || await db.Domains.CountAsync(d=>d.TenantId==id && d.IsActive,ct)>plan.MaxDomains) throw new TenantRuleException("PlanBelowCurrentUsage");
            var allowed=await db.Features.Where(f=>f.PlanCode==plan.Code && f.Enabled).Select(f=>f.Feature).ToListAsync(ct);
            if(await db.Modules.AnyAsync(m=>m.TenantId==id && m.Enabled && !allowed.Contains(m.Module),ct)) throw new TenantRuleException("DisableUnsupportedModulesFirst");
            var subscription=await db.Subscriptions.SingleAsync(s=>s.TenantId==id,ct);subscription.PlanCode=plan.Code;subscription.StartsAt=request.StartsAt;subscription.EndsAt=request.EndsAt;subscription.Status=request.Status;
        },ct);
    }
    public async Task<IReadOnlyCollection<LocationView>> Locations(Guid id,CancellationToken ct)
    {await Load(id,false,ct);return await db.Locations.AsNoTracking().Where(l=>l.TenantId==id).OrderBy(l=>l.Name).Select(l=>new LocationView(l.Id,l.Name,l.Address,l.CountryCode,l.IsActive)).ToListAsync(ct);}
    public Task<TenantView> AddLocation(Guid id,LocationRequest request,CancellationToken ct)=>Mutate(id,request.Version,"tenant.location.added",async _=>
    {
        var (_,plan,_)=await Plan(id,ct);if(await db.Locations.CountAsync(l=>l.TenantId==id && l.IsActive,ct)>=plan.MaxLocations) throw new TenantRuleException("LocationLimitExceeded");
        if(request.CountryCode.Any(c=>c is <'A' or >'Z')) throw new TenantRuleException("InvalidCountry",400);
        db.Locations.Add(new TenantLocation{TenantId=id,Name=TenantRules.Name(request.Name),Address=TenantRules.Name(request.Address,500),CountryCode=request.CountryCode});
    },ct);
    public Task<TenantView> DeactivateLocation(Guid id,Guid locationId,VersionRequest request,CancellationToken ct)=>Mutate(id,request.Version,"tenant.location.deactivated",async _=>
    {var item=await db.Locations.SingleOrDefaultAsync(l=>l.Id==locationId && l.TenantId==id,ct) ?? throw new TenantRuleException("LocationNotFound",404);item.IsActive=false;},ct);
    public async Task<IReadOnlyCollection<DomainView>> Domains(Guid id,CancellationToken ct)
    {await Load(id,false,ct);return await db.Domains.AsNoTracking().Where(d=>d.TenantId==id).OrderBy(d=>d.Host).Select(d=>new DomainView(d.Id,d.Host,d.VerifiedAt,d.IsPrimary,d.IsActive)).ToListAsync(ct);}
    public async Task<DomainChallenge> AddDomain(Guid id,DomainRequest request,CancellationToken ct)
    {
        TenantDomain? domain=null;var token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));var host=TenantRules.Domain(request.Host);
        var view=await Mutate(id,request.Version,"tenant.domain.challenge",async _=>
        {
            var existing=await db.Domains.IgnoreQueryFilters().SingleOrDefaultAsync(d=>d.Host==host,ct);
            if(existing is not null && existing.TenantId!=id) throw new TenantRuleException("DomainUnavailable");
            if(existing is {IsActive:true,VerifiedAt:not null}) throw new TenantRuleException("DomainAlreadyVerified");
            var (_,plan,_)=await Plan(id,ct);
            if(existing is not {IsActive:true} && await db.Domains.CountAsync(d=>d.TenantId==id && d.IsActive,ct)>=plan.MaxDomains) throw new TenantRuleException("DomainLimitExceeded");
            domain=existing??new TenantDomain{TenantId=id,Host=host};if(existing is null) db.Domains.Add(domain);
            domain.IsActive=true;domain.IsPrimary=false;domain.VerifiedAt=null;domain.VerificationHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));domain.VerificationExpiresAt=DateTimeOffset.UtcNow.AddDays(7);
        },ct);
        return new(domain!.Id,host,"_srecommerce-verification."+host,token,domain.VerificationExpiresAt,view.Version);
    }
    public async Task<TenantView> VerifyDomain(Guid id,Guid domainId,DomainVerificationRequest request,CancellationToken ct)
    {
        await Load(id,true,ct);var domain=await db.Domains.AsNoTracking().SingleOrDefaultAsync(d=>d.Id==domainId && d.TenantId==id,ct) ?? throw new TenantRuleException("DomainNotFound",404);
        if(!domain.IsActive || domain.VerificationExpiresAt<=DateTimeOffset.UtcNow || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(request.Token)),Convert.FromHexString(domain.VerificationHash))) throw new TenantRuleException("InvalidDomainChallenge",400);
        var records=await dns.GetTxtAsync("_srecommerce-verification."+domain.Host,ct);
        if(!records.Contains("srecommerce-verification="+request.Token,StringComparer.Ordinal)) throw new TenantRuleException("DomainOwnershipNotProven");
        return await Mutate(id,request.Version,"tenant.domain.verified",async _=>
        {
            var item=await db.Domains.SingleAsync(d=>d.Id==domainId && d.TenantId==id,ct);
            if(item.VerificationHash!=domain.VerificationHash || !item.IsActive) throw new TenantRuleException("DomainChallengeChanged");
            if(request.MakePrimary)
            {
                foreach(var prior in await db.Domains.Where(d=>d.TenantId==id && d.IsPrimary).ToListAsync(ct)) prior.IsPrimary=false;
                await db.SaveChangesAsync(ct);
            }
            item.VerifiedAt=DateTimeOffset.UtcNow;item.IsPrimary=request.MakePrimary;
        },ct);
    }
    public Task<TenantView> DeactivateDomain(Guid id,Guid domainId,VersionRequest request,CancellationToken ct)=>Mutate(id,request.Version,"tenant.domain.deactivated",async _=>
    {var item=await db.Domains.SingleOrDefaultAsync(d=>d.Id==domainId && d.TenantId==id,ct) ?? throw new TenantRuleException("DomainNotFound",404);item.IsActive=false;item.IsPrimary=false;},ct);
    public async Task<object> Audit(Guid id,int page,CancellationToken ct)
    {
        Page(page);await Load(id,true,ct);
        if(Caller.IsPlatformAdmin){db.Audit.Add(new TenantAudit{TenantId=id,ActorUserId=Caller.UserId,Action="platform.audit.read",PlatformOperation=true});await db.SaveChangesAsync(ct);}
        return await db.Audit.AsNoTracking().Where(a=>a.TenantId==id).OrderByDescending(a=>a.Id).Skip((page-1)*50).Take(50).Select(a=>new{a.Id,a.ActorUserId,a.Action,a.PlatformOperation,a.CreatedAt}).ToListAsync(ct);
    }
    public async Task<object> PlatformHistory(int page,CancellationToken ct)
    {
        Admin();Page(page);db.PlatformAudit.Add(new TenantPlatformAudit{ActorUserId=Caller.UserId,Action="platform.audit.read",Resource="page:"+page});await db.SaveChangesAsync(ct);
        return await db.PlatformAudit.AsNoTracking().OrderByDescending(a=>a.Id).Skip((page-1)*50).Take(50).Select(a=>new{a.Id,a.ActorUserId,a.Action,a.Resource,a.CreatedAt}).ToListAsync(ct);
    }
}
