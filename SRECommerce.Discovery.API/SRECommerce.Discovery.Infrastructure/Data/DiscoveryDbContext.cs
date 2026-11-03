using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Discovery.Domain;

namespace SRECommerce.Discovery.Infrastructure;

public sealed class DiscoveryDbContext(DbContextOptions<DiscoveryDbContext> options, ModuleScope scope) : DbContext(options), IModuleStore
{
    public Guid? Tenant => scope.TenantId;
    public DbContext Context => this;
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<SearchDocument> Documents => Set<SearchDocument>();
    public DbSet<ProductReview> Reviews => Set<ProductReview>();
    public DbSet<ReviewDecision> Decisions => Set<ReviewDecision>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();
        model.Entity<AuditEntry>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<InboxMessage>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<OutboxMessage>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<SearchDocument>(e =>
        {
            e.ToTable("SearchDocuments", t => t.HasCheckConstraint("CK_SearchDocuments_Attributes", "ISJSON([Attributes])=1 AND [Revision]>0"));
            e.HasKey(x => x.Id); e.TenantEntity(x => x.TenantId == Tenant);
            e.HasIndex(x => new { x.TenantId, x.ProductId }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.CategoryId, x.BrandId });
            e.Property(x => x.Name).HasMaxLength(200); e.Property(x => x.SearchText).HasMaxLength(6000); e.Property(x => x.Attributes).HasMaxLength(16000);
        });
        model.Entity<ProductReview>(e =>
        {
            e.ToTable("ProductReviews", t => t.HasCheckConstraint("CK_ProductReviews_State", "[Rating] BETWEEN 1 AND 5 AND [State] BETWEEN 0 AND 2"));
            e.HasKey(x => new { x.TenantId, x.Id }); e.TenantEntity(x => x.TenantId == Tenant);
            e.HasIndex(x => new { x.TenantId, x.ProductId, x.UserId }).IsUnique(); e.HasIndex(x => new { x.TenantId, x.State, x.ProductId, x.UpdatedAt });
            e.Property(x => x.Body).HasMaxLength(1500); e.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<ReviewDecision>(e =>
        {
            e.ToTable("ReviewDecisions"); e.HasKey(x => new { x.TenantId, x.Id }); e.TenantEntity(x => x.TenantId == Tenant);
            e.HasOne<ProductReview>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.ReviewId }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Reason).HasMaxLength(300);
        });
    }
    private void Guard()
    {
        this.GuardWrites(scope);
        if (ChangeTracker.Entries<ReviewDecision>().Any(e => e.State == EntityState.Modified)) throw new BusinessFault("ReviewDecisionImmutable", 403);
    }
    public override int SaveChanges(bool accept) { Guard(); return base.SaveChanges(accept); }
    public override Task<int> SaveChangesAsync(bool accept, CancellationToken ct = default) { Guard(); return base.SaveChangesAsync(accept, ct); }
}

public sealed class DiscoveryDbContextFactory : IDesignTimeDbContextFactory<DiscoveryDbContext>
{
    public DiscoveryDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<DiscoveryDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Discovery"))).Options, new ModuleScope());
}
