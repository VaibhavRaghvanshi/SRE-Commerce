using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;

public sealed class TenantDbContext(DbContextOptions<TenantDbContext> options,ITenantCaller caller,TenantDataScope scope) : DbContext(options)
{
    public Guid UserId => caller.Current.UserId;
    public Guid? CurrentTenant => scope.TenantId ?? caller.Current.SelectedTenantId;
    public bool Platform => caller.Current.IsPlatformAdmin;
    public DbSet<BusinessTenant> Tenants => Set<BusinessTenant>();
    public DbSet<BusinessType> BusinessTypes => Set<BusinessType>();
    public DbSet<TenantSettings> Settings => Set<TenantSettings>();
    public DbSet<TenantModule> Modules => Set<TenantModule>();
    public DbSet<SubscriptionPlan> Plans => Set<SubscriptionPlan>();
    public DbSet<PlanFeature> Features => Set<PlanFeature>();
    public DbSet<TenantSubscription> Subscriptions => Set<TenantSubscription>();
    public DbSet<TenantLocation> Locations => Set<TenantLocation>();
    public DbSet<TenantDomain> Domains => Set<TenantDomain>();
    public DbSet<TenantAudit> Audit => Set<TenantAudit>();
    public DbSet<TenantOutbox> Outbox => Set<TenantOutbox>();
    public DbSet<TenantPlatformAudit> PlatformAudit => Set<TenantPlatformAudit>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<BusinessTenant>(e=>
        {
            e.ToTable("Tenants"); e.HasKey(x=>x.Id); e.Property(x=>x.Name).HasMaxLength(160); e.Property(x=>x.Slug).HasMaxLength(63);
            e.Property(x=>x.BusinessTypeCode).HasMaxLength(40); e.Property(x=>x.CustomBusinessType).HasMaxLength(100);
            e.Property(x=>x.RegistrationFingerprint).HasMaxLength(64); e.Property(x=>x.SuspensionReason).HasMaxLength(300); e.Property(x=>x.Version).IsRowVersion();
            e.HasIndex(x=>x.Slug).IsUnique(); e.HasIndex(x=>new{x.OwnerUserId,x.RegistrationRequestId}).IsUnique(); e.HasIndex(x=>new{x.OwnerUserId,x.Status});
            e.HasOne<BusinessType>().WithMany().HasForeignKey(x=>x.BusinessTypeCode).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(x=>Platform || x.Id==CurrentTenant || (x.OwnerUserId==UserId && (x.Status==TenantStatus.Draft || x.Status==TenantStatus.PendingApproval)));
        });
        model.Entity<BusinessType>(e=>{e.ToTable("BusinessTypes");e.HasKey(x=>x.Code);e.Property(x=>x.Code).HasMaxLength(40);e.Property(x=>x.Name).HasMaxLength(100);});
        model.Entity<TenantSettings>(e=>{e.ToTable("TenantSettings");e.HasKey(x=>x.TenantId);e.Property(x=>x.Currency).HasMaxLength(3);e.Property(x=>x.Culture).HasMaxLength(20);e.Property(x=>x.TimeZone).HasMaxLength(100);e.Property(x=>x.DisplayName).HasMaxLength(160);e.Property(x=>x.LogoUrl).HasMaxLength(500);});
        model.Entity<TenantModule>(e=>{e.ToTable("TenantModules");e.HasKey(x=>new{x.TenantId,x.Module});e.Property(x=>x.Module).HasMaxLength(40);});
        model.Entity<SubscriptionPlan>(e=>{e.ToTable("SubscriptionPlans");e.HasKey(x=>x.Code);e.Property(x=>x.Code).HasMaxLength(40);e.Property(x=>x.Name).HasMaxLength(100);});
        model.Entity<PlanFeature>(e=>{e.ToTable("PlanFeatures");e.HasKey(x=>new{x.PlanCode,x.Feature});e.Property(x=>x.PlanCode).HasMaxLength(40);e.Property(x=>x.Feature).HasMaxLength(60);e.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x=>x.PlanCode).OnDelete(DeleteBehavior.Restrict);});
        model.Entity<TenantSubscription>(e=>{e.ToTable("TenantSubscriptions");e.HasKey(x=>x.TenantId);e.Property(x=>x.PlanCode).HasMaxLength(40);e.HasOne<SubscriptionPlan>().WithMany().HasForeignKey(x=>x.PlanCode).OnDelete(DeleteBehavior.Restrict);});
        model.Entity<TenantLocation>(e=>{e.ToTable("TenantLocations");e.HasKey(x=>x.Id);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Address).HasMaxLength(500);e.Property(x=>x.CountryCode).HasMaxLength(2);e.HasIndex(x=>new{x.TenantId,x.IsActive});});
        model.Entity<TenantDomain>(e=>{e.ToTable("TenantDomains");e.HasKey(x=>x.Id);e.Property(x=>x.Host).HasMaxLength(253);e.Property(x=>x.VerificationHash).HasMaxLength(64);e.HasIndex(x=>x.Host).IsUnique();e.HasIndex(x=>x.TenantId).IsUnique().HasFilter("[IsPrimary] = 1 AND [IsActive] = 1");});
        model.Entity<TenantAudit>(e=>{e.ToTable("TenantAuditHistory");e.HasKey(x=>x.Id);e.Property(x=>x.Action).HasMaxLength(100);e.HasIndex(x=>new{x.TenantId,x.CreatedAt});});
        model.Entity<TenantOutbox>(e=>{e.ToTable("TenantOutbox");e.HasKey(x=>x.Id);e.Property(x=>x.Type).HasMaxLength(100);e.Property(x=>x.Payload).HasMaxLength(1000);e.HasIndex(x=>new{x.DispatchedAt,x.CreatedAt});});
        model.Entity<TenantPlatformAudit>(e=>{e.ToTable("TenantPlatformAudit");e.HasKey(x=>x.Id);e.Property(x=>x.Action).HasMaxLength(100);e.Property(x=>x.Resource).HasMaxLength(100);e.HasIndex(x=>x.CreatedAt);});
        ConfigureTenant<TenantSettings>(model); ConfigureTenant<TenantModule>(model); ConfigureTenant<TenantSubscription>(model);
        ConfigureTenant<TenantLocation>(model); ConfigureTenant<TenantDomain>(model); ConfigureTenant<TenantAudit>(model); ConfigureTenant<TenantOutbox>(model);
    }
    private void ConfigureTenant<T>(ModelBuilder model) where T:class,ITenantRecord
    {
        model.Entity<T>().HasQueryFilter(x=>Platform || x.TenantId==CurrentTenant);
        model.Entity<T>().HasOne<BusinessTenant>().WithMany().HasForeignKey(x=>x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
    public override Task<int> SaveChangesAsync(CancellationToken ct=default)
    {
        foreach(var entry in ChangeTracker.Entries<BusinessTenant>().Where(e=>e.State is EntityState.Added or EntityState.Modified))
            if(!Platform && (entry.Entity.Id!=CurrentTenant || (entry.State==EntityState.Added && entry.Entity.OwnerUserId!=UserId)))
                throw new TenantRuleException("TenantWriteScopeMismatch",403);
        foreach(var entry in ChangeTracker.Entries<ITenantRecord>().Where(e=>e.State is EntityState.Added or EntityState.Modified))
            if(!Platform && entry.Entity.TenantId!=CurrentTenant) throw new TenantRuleException("TenantWriteScopeMismatch",403);
        return base.SaveChangesAsync(ct);
    }
}
public sealed class DesignCaller : ITenantCaller { public TenantCaller Current => new(Guid.Empty,"",false,null,null); }
public sealed class TenantDbContextFactory : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var connection=Environment.GetEnvironmentVariable("TENANT_MIGRATION_CONNECTION") ?? "Server=localhost,1433;Database=SRECommerce_TenantDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
        var database=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connection).InitialCatalog;
        if(database!="SRECommerce_TenantDB" && !database.StartsWith("SRECommerce_TenantTests_",StringComparison.Ordinal)) throw new InvalidOperationException("Wrong Tenant migration database.");
        return new TenantDbContext(new DbContextOptionsBuilder<TenantDbContext>().UseSqlServer(connection).Options,new DesignCaller(),new TenantDataScope());
    }
}
