using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Manufacturing.Domain;

namespace SRECommerce.Manufacturing.Infrastructure;

public sealed class ManufacturingDbContext(DbContextOptions<ManufacturingDbContext> options, ModuleScope scope) : DbContext(options), IModuleStore
{
    public Guid? Tenant => scope.TenantId;
    public DbContext Context => this;
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<BillOfMaterials> Boms => Set<BillOfMaterials>();
    public DbSet<ProductionOrder> Orders => Set<ProductionOrder>();
    public DbSet<ProductionHistory> History => Set<ProductionHistory>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();
        model.Entity<OutboxMessage>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<InboxMessage>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<AuditEntry>().TenantEntity(x => x.TenantId == Tenant);
        model.Entity<BillOfMaterials>(e =>
        {
            e.ToTable("BillsOfMaterials", t => t.HasCheckConstraint("CK_Bom_Document", "ISJSON([Materials])=1 AND ISJSON([Stages])=1 AND [Revision]>0"));
            e.HasKey(x => new { x.TenantId, x.Id }); e.TenantEntity(x => x.TenantId == Tenant);
            e.HasIndex(x => new { x.TenantId, x.Code, x.Revision }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(48); e.Property(x => x.Name).HasMaxLength(160); e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Materials).HasMaxLength(12000); e.Property(x => x.Stages).HasMaxLength(4096);
        });
        model.Entity<ProductionOrder>(e =>
        {
            e.ToTable("ProductionOrders", t =>
            {
                t.HasCheckConstraint("CK_Production_Document", "ISJSON([Materials])=1 AND ISJSON([Stages])=1");
                t.HasCheckConstraint("CK_Production_Workflow", "[State] BETWEEN 0 AND 8 AND [Attempts]>=0 AND [StagesCompleted] BETWEEN 0 AND 12");
                t.HasCheckConstraint("CK_Production_Quantities", "[PlannedQuantity]>0 AND [GoodQuantity]>=0 AND [WasteQuantity]>=0 AND [GoodQuantity]+[WasteQuantity]<=[PlannedQuantity] AND [StandardMaterialCost]>=0 AND [OverheadCost]>=0");
            });
            e.HasKey(x => new { x.TenantId, x.Id }); e.TenantEntity(x => x.TenantId == Tenant); e.Property(x => x.Version).IsRowVersion();
            e.HasOne<BillOfMaterials>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.BomId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.State, x.NextAttemptAt, x.LeaseUntil }); e.HasIndex(x => new { x.TenantId, x.CreatedAt });
            e.Property(x => x.Currency).HasMaxLength(3); e.Property(x => x.OutputLot).HasMaxLength(64); e.Property(x => x.RequestHash).HasMaxLength(64);
            e.Property(x => x.LastFault).HasMaxLength(80); e.Property(x => x.Materials).HasMaxLength(12000); e.Property(x => x.Stages).HasMaxLength(4096);
            e.Property(x => x.PlannedQuantity).HasPrecision(18, 3); e.Property(x => x.GoodQuantity).HasPrecision(18, 3); e.Property(x => x.WasteQuantity).HasPrecision(18, 3);
            e.Property(x => x.StandardMaterialCost).HasPrecision(18, 4); e.Property(x => x.OverheadCost).HasPrecision(18, 4);
        });
        model.Entity<ProductionHistory>(e =>
        {
            e.ToTable("ProductionHistory"); e.HasKey(x => new { x.TenantId, x.Id }); e.TenantEntity(x => x.TenantId == Tenant);
            e.HasOne<ProductionOrder>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.OrderId }).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.TenantId, x.OrderId, x.CreatedAt }); e.Property(x => x.Action).HasMaxLength(64); e.Property(x => x.Note).HasMaxLength(300);
        });
    }
    private void Guard()
    {
        this.GuardWrites(scope);
        if (ChangeTracker.Entries().Any(e => e.Entity is BillOfMaterials or ProductionHistory && e.State == EntityState.Modified))
            throw new BusinessFault("ProductionHistoryImmutable", 403);
        string[] snapshot = [nameof(ProductionOrder.BomId), nameof(ProductionOrder.WarehouseId), nameof(ProductionOrder.OutputVariantId),
            nameof(ProductionOrder.Materials), nameof(ProductionOrder.Stages), nameof(ProductionOrder.PlannedQuantity), nameof(ProductionOrder.Currency),
            nameof(ProductionOrder.StandardMaterialCost), nameof(ProductionOrder.RequestHash), nameof(ProductionOrder.CreatedAt)];
        foreach (var e in ChangeTracker.Entries<ProductionOrder>().Where(e => e.State == EntityState.Modified))
        {
            if (e.Properties.Any(p => p.IsModified && snapshot.Contains(p.Metadata.Name)) ||
                e.OriginalValues.GetValue<string?>(nameof(ProductionOrder.OutputLot)) is not null &&
                e.Properties.Any(p => p.IsModified && p.Metadata.Name is nameof(ProductionOrder.OutputLot) or nameof(ProductionOrder.OutputExpiresAt) or nameof(ProductionOrder.GoodQuantity) or nameof(ProductionOrder.WasteQuantity)))
                throw new BusinessFault("ProductionSnapshotImmutable", 403);
        }
    }
    public override int SaveChanges(bool accept) { Guard(); return base.SaveChanges(accept); }
    public override Task<int> SaveChangesAsync(bool accept, CancellationToken ct = default) { Guard(); return base.SaveChangesAsync(accept, ct); }
}

public sealed class ManufacturingDbContextFactory : IDesignTimeDbContextFactory<ManufacturingDbContext>
{
    public ManufacturingDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<ManufacturingDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Manufacturing"))).Options, new ModuleScope());
}
