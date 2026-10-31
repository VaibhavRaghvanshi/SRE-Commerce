using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Infrastructure;
public sealed class ThemeDbContext(DbContextOptions<ThemeDbContext> options,ModuleScope scope):DbContext(options),IModuleStore
{
    public Guid? CurrentTenant=>scope.TenantId;
    public DbContext Context=>this;public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>();public DbSet<InboxMessage> Inbox=>Set<InboxMessage>();public DbSet<AuditEntry> Audit=>Set<AuditEntry>();
    public DbSet<ThemeDraft> Themes=>Set<ThemeDraft>();public DbSet<ThemeVersion> Versions=>Set<ThemeVersion>();public DbSet<TenantTheme> Published=>Set<TenantTheme>();public DbSet<ThemePublication> Publications=>Set<ThemePublication>();public DbSet<ThemePreset> Presets=>Set<ThemePreset>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();
        model.Entity<OutboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<InboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<AuditEntry>().TenantEntity(x=>x.TenantId==CurrentTenant);
        model.Entity<ThemeDraft>(e=>{e.ToTable("Themes",t=>t.HasCheckConstraint("CK_Themes_Json","ISJSON([Document]) = 1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Document).HasMaxLength(65536);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Name}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ThemeVersion>(e=>{e.ToTable("ThemeVersions",t=>t.HasCheckConstraint("CK_ThemeVersions_Json","ISJSON([Document]) = 1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Document).HasMaxLength(65536);e.Property(x=>x.Hash).HasMaxLength(64);e.HasIndex(x=>new{x.TenantId,x.ThemeId,x.Revision}).IsUnique();e.HasOne<ThemeDraft>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ThemeId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<TenantTheme>(e=>{e.ToTable("TenantThemes");e.HasKey(x=>x.TenantId);e.Property(x=>x.Version).IsRowVersion();e.HasOne<ThemeVersion>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ThemeVersionId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ThemePublication>(e=>{e.ToTable("ThemePublishHistory");e.HasKey(x=>new{x.TenantId,x.Id});e.HasIndex(x=>new{x.TenantId,x.Sequence}).IsUnique();e.HasOne<ThemeVersion>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ThemeVersionId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ThemePreset>(e=>{e.ToTable("ThemePresets",t=>t.HasCheckConstraint("CK_ThemePresets_Json","ISJSON([Document]) = 1"));e.HasKey(x=>x.BusinessType);e.Property(x=>x.BusinessType).HasMaxLength(40);e.Property(x=>x.Document).HasMaxLength(65536);e.Property(x=>x.Version).IsRowVersion();e.HasAnnotation("SRECommerce:GlobalCatalog",true);});
    }
    private void Guard()
    {
        this.GuardWrites(scope);
        if(ChangeTracker.Entries().Any(e=>(e.Entity is ThemeVersion or ThemePublication)&&e.State is EntityState.Modified or EntityState.Deleted))throw new BusinessFault("PublishedHistoryImmutable",403);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess){Guard();return base.SaveChanges(acceptAllChangesOnSuccess);}
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,CancellationToken ct=default){Guard();return base.SaveChangesAsync(acceptAllChangesOnSuccess,ct);}
}
public sealed class ThemeDbContextFactory:IDesignTimeDbContextFactory<ThemeDbContext>
{public ThemeDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<ThemeDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Theme"))).Options,new ModuleScope());}
