using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Infrastructure;
public sealed class StorefrontDbContext(DbContextOptions<StorefrontDbContext> options,ModuleScope scope):DbContext(options),IModuleStore
{
    public Guid? CurrentTenant=>scope.TenantId;public DbContext Context=>this;
    public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>();public DbSet<InboxMessage> Inbox=>Set<InboxMessage>();public DbSet<AuditEntry> Audit=>Set<AuditEntry>();
    public DbSet<StorefrontPage> Pages=>Set<StorefrontPage>();public DbSet<LayoutVersion> Versions=>Set<LayoutVersion>();public DbSet<PublishedConfiguration> Published=>Set<PublishedConfiguration>();public DbSet<LayoutPublication> Publications=>Set<LayoutPublication>();public DbSet<LayoutTemplate> Templates=>Set<LayoutTemplate>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();model.Entity<OutboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<InboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<AuditEntry>().TenantEntity(x=>x.TenantId==CurrentTenant);
        model.Entity<StorefrontPage>(e=>{e.ToTable("StorefrontPages",t=>t.HasCheckConstraint("CK_StorefrontPages_Json","ISJSON([Document]) = 1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Slug).HasMaxLength(80);e.Property(x=>x.Title).HasMaxLength(160);e.Property(x=>x.Document).HasMaxLength(131072);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Slug}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<LayoutVersion>(e=>{e.ToTable("LayoutVersions",t=>t.HasCheckConstraint("CK_LayoutVersions_Json","ISJSON([Document]) = 1"));e.HasKey(x=>new{x.TenantId,x.Id});e.HasAlternateKey(x=>new{x.TenantId,x.PageId,x.Id});e.Property(x=>x.Document).HasMaxLength(131072);e.Property(x=>x.Hash).HasMaxLength(64);e.HasIndex(x=>new{x.TenantId,x.PageId,x.Revision}).IsUnique();e.HasOne<StorefrontPage>().WithMany().HasForeignKey(x=>new{x.TenantId,x.PageId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<PublishedConfiguration>(e=>{e.ToTable("PublishedConfigurations");e.HasKey(x=>new{x.TenantId,x.PageId});e.Property(x=>x.Version).IsRowVersion();e.HasOne<LayoutVersion>().WithMany().HasForeignKey(x=>new{x.TenantId,x.PageId,x.LayoutVersionId}).HasPrincipalKey(x=>new{x.TenantId,x.PageId,x.Id}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<LayoutPublication>(e=>{e.ToTable("LayoutPublishHistory");e.HasKey(x=>new{x.TenantId,x.Id});e.HasIndex(x=>new{x.TenantId,x.PageId,x.Sequence}).IsUnique();e.HasOne<LayoutVersion>().WithMany().HasForeignKey(x=>new{x.TenantId,x.PageId,x.LayoutVersionId}).HasPrincipalKey(x=>new{x.TenantId,x.PageId,x.Id}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<LayoutTemplate>(e=>{e.ToTable("LayoutTemplates",t=>t.HasCheckConstraint("CK_LayoutTemplates_Json","ISJSON([Document]) = 1"));e.HasKey(x=>x.BusinessType);e.Property(x=>x.BusinessType).HasMaxLength(40);e.Property(x=>x.Document).HasMaxLength(131072);e.Property(x=>x.Version).IsRowVersion();e.HasAnnotation("SRECommerce:GlobalCatalog",true);});
    }
    private void Guard()
    {this.GuardWrites(scope);if(ChangeTracker.Entries().Any(e=>(e.Entity is LayoutVersion or LayoutPublication)&&e.State is EntityState.Modified or EntityState.Deleted))throw new BusinessFault("PublishedHistoryImmutable",403);}
    public override int SaveChanges(bool acceptAllChangesOnSuccess){Guard();return base.SaveChanges(acceptAllChangesOnSuccess);}
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,CancellationToken ct=default){Guard();return base.SaveChangesAsync(acceptAllChangesOnSuccess,ct);}
}
