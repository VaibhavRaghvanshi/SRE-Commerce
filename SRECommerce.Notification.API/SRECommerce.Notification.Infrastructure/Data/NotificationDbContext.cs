using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Infrastructure;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options, ModuleScope scope) : DbContext(options), IModuleStore
{
    public Guid? Tenant => scope.TenantId;
    public DbContext Context => this;
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<NotificationTemplate> Templates => Set<NotificationTemplate>();
    public DbSet<NotificationPreference> Preferences => Set<NotificationPreference>();
    public DbSet<NotificationJob> Jobs => Set<NotificationJob>();
    public DbSet<NotificationDelivery> Deliveries => Set<NotificationDelivery>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();
        model.Entity<AuditEntry>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<OutboxMessage>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<InboxMessage>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<NotificationTemplate>(e =>
        {
            e.ToTable("NotificationTemplates"); e.HasKey(x => new { x.TenantId, x.Kind, x.Channel }); e.TenantEntity(x => x.TenantId == Tenant);
            e.Property(x => x.Subject).HasMaxLength(160); e.Property(x => x.Body).HasMaxLength(2000); e.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<NotificationPreference>(e =>
        {
            e.ToTable("NotificationPreferences"); e.HasKey(x => new { x.TenantId, x.UserId }); e.TenantEntity(x => x.TenantId == Tenant); e.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<NotificationJob>(e =>
        {
            e.ToTable("NotificationJobs", t => t.HasCheckConstraint("CK_NotificationJobs_State", "[State] BETWEEN 0 AND 5 AND [Attempts] BETWEEN 0 AND 5 AND [Channel] BETWEEN 0 AND 2"));
            e.HasKey(x => new { x.TenantId, x.EventId, x.Channel }); e.TenantEntity(x => x.TenantId == Tenant);
            e.HasIndex(x => new { x.State, x.NextAttemptAt, x.LeaseUntil }); e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.Property(x => x.Recipient).HasMaxLength(254); e.Property(x => x.Subject).HasMaxLength(200); e.Property(x => x.Body).HasMaxLength(2400); e.Property(x => x.Version).IsRowVersion();
        });
        model.Entity<NotificationDelivery>(e =>
        {
            e.ToTable("NotificationDeliveries"); e.HasKey(x => new { x.TenantId, x.EventId, x.Channel, x.Attempt }); e.TenantEntity(x => x.TenantId == Tenant);
            e.HasOne<NotificationJob>().WithMany().HasForeignKey(x => new { x.TenantId, x.EventId, x.Channel }).OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Code).HasMaxLength(50);
        });
    }

    private void Guard()
    {
        this.GuardWrites(scope);
        if (ChangeTracker.Entries().Any(e => e.Entity is NotificationDelivery or InboxMessage && e.State == EntityState.Modified))
            throw new BusinessFault("NotificationHistoryImmutable", 403);
    }
    public override int SaveChanges(bool acceptAllChangesOnSuccess) { Guard(); return base.SaveChanges(acceptAllChangesOnSuccess); }
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    { Guard(); return base.SaveChangesAsync(acceptAllChangesOnSuccess, ct); }
}

public sealed class NotificationDbContextFactory : IDesignTimeDbContextFactory<NotificationDbContext>
{
    public NotificationDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<NotificationDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Notification"))).Options, new ModuleScope());
}
