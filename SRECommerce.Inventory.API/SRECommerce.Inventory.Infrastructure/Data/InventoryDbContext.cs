using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Infrastructure;
public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options,ModuleScope scope):DbContext(options),IModuleStore
{
    public Guid? CurrentTenant=>scope.TenantId;
    public DbContext Context=>this;
    public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox=>Set<InboxMessage>();
    public DbSet<AuditEntry> Audit=>Set<AuditEntry>();
    public DbSet<Branch> Branches=>Set<Branch>();
    public DbSet<Warehouse> Warehouses=>Set<Warehouse>();
    public DbSet<StockItem> Items=>Set<StockItem>();
    public DbSet<InventoryBatch> Batches=>Set<InventoryBatch>();
    public DbSet<StockBalance> Balances=>Set<StockBalance>();
    public DbSet<InventoryMovement> Movements=>Set<InventoryMovement>();
    public DbSet<StockOperation> Operations=>Set<StockOperation>();
    public DbSet<StockReservation> Reservations=>Set<StockReservation>();
    public DbSet<Supplier> Suppliers=>Set<Supplier>();
    public DbSet<PurchaseOrder> Purchases=>Set<PurchaseOrder>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();model.Entity<OutboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<InboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<AuditEntry>().TenantEntity(x=>x.TenantId==CurrentTenant);
        model.Entity<Branch>(e=>{e.ToTable("Branches");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<Warehouse>(e=>{e.ToTable("Warehouses");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.HasOne<Branch>().WithMany().HasForeignKey(x=>new{x.TenantId,x.BranchId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<StockItem>(e=>{e.ToTable("StockItems",t=>t.HasCheckConstraint("CK_StockItems_ReorderLevel","[ReorderLevel]>=0"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Sku).HasMaxLength(64);e.Property(x=>x.Barcode).HasMaxLength(64);e.Property(x=>x.ReorderLevel).HasPrecision(18,3);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Sku}).IsUnique();e.HasIndex(x=>new{x.TenantId,x.Barcode});e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<InventoryBatch>(e=>{e.ToTable("InventoryBatches");e.HasKey(x=>new{x.TenantId,x.Id});e.HasAlternateKey(x=>new{x.TenantId,x.Id,x.WarehouseId,x.VariantId});e.Property(x=>x.Lot).HasMaxLength(64);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.WarehouseId,x.VariantId,x.Lot}).IsUnique();e.HasIndex(x=>new{x.TenantId,x.ExpiresAt});e.HasOne<Warehouse>().WithMany().HasForeignKey(x=>new{x.TenantId,x.WarehouseId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<StockItem>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.VariantId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<StockBalance>(e=>{e.ToTable("InventoryBalances",t=>t.HasCheckConstraint("CK_InventoryBalances_Quantities","[OnHand]>=0 AND [Reserved]>=0 AND [Damaged]>=0 AND [Reserved]<=[OnHand]"));e.HasKey(x=>new{x.TenantId,x.Id});e.HasAlternateKey(x=>new{x.TenantId,x.Id,x.VariantId});e.Property(x=>x.OnHand).HasPrecision(18,3);e.Property(x=>x.Reserved).HasPrecision(18,3);e.Property(x=>x.Damaged).HasPrecision(18,3);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.WarehouseId,x.VariantId,x.BatchId}).IsUnique();e.HasOne<InventoryBatch>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.BatchId,x.WarehouseId,x.VariantId}).HasPrincipalKey(x=>new{x.TenantId,x.Id,x.WarehouseId,x.VariantId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<InventoryMovement>(e=>{e.ToTable("InventoryMovements");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.OnHandDelta).HasPrecision(18,3);e.Property(x=>x.ReservedDelta).HasPrecision(18,3);e.Property(x=>x.DamagedDelta).HasPrecision(18,3);e.Property(x=>x.Reason).HasMaxLength(300);e.HasIndex(x=>new{x.TenantId,x.BalanceId,x.OccurredAt});e.HasIndex(x=>new{x.TenantId,x.OperationId});e.HasOne<StockBalance>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.BalanceId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<StockOperation>(e=>{e.ToTable("StockOperations",t=>t.HasCheckConstraint("CK_StockOperations_Result","ISJSON([Result])=1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.PayloadHash).HasMaxLength(64);e.Property(x=>x.Result).HasMaxLength(65536);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<StockReservation>(e=>{e.ToTable("StockReservations");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Reference).HasMaxLength(120);e.Property(x=>x.PayloadHash).HasMaxLength(64);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Status,x.ExpiresAt});e.HasIndex(x=>new{x.Status,x.ExpiresAt});e.HasOne<Warehouse>().WithMany().HasForeignKey(x=>new{x.TenantId,x.WarehouseId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ReservationLine>(e=>{e.ToTable("StockReservationLines",t=>t.HasCheckConstraint("CK_ReservationLines_Quantity","[Quantity]>0"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Quantity).HasPrecision(18,3);e.HasIndex(x=>new{x.TenantId,x.ReservationId,x.BalanceId}).IsUnique();e.HasOne<StockReservation>().WithMany(x=>x.Lines).HasForeignKey(x=>new{x.TenantId,Id=x.ReservationId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<StockBalance>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.BalanceId,x.VariantId}).HasPrincipalKey(x=>new{x.TenantId,x.Id,x.VariantId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<Supplier>(e=>{e.ToTable("Suppliers");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.ContactEmail).HasMaxLength(254);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<PurchaseOrder>(e=>{e.ToTable("PurchaseOrders");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Reference).HasMaxLength(120);e.Property(x=>x.Currency).HasMaxLength(3);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Reference}).IsUnique();e.HasOne<Supplier>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.SupplierId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<Warehouse>().WithMany().HasForeignKey(x=>new{x.TenantId,x.WarehouseId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<PurchaseOrderLine>(e=>{e.ToTable("PurchaseOrderLines",t=>t.HasCheckConstraint("CK_PurchaseLines_Quantities","[Ordered]>0 AND [Received]>=0 AND [Received]<=[Ordered] AND [UnitCost]>=0"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Ordered).HasPrecision(18,3);e.Property(x=>x.Received).HasPrecision(18,3);e.Property(x=>x.UnitCost).HasPrecision(18,4);e.HasIndex(x=>new{x.TenantId,x.PurchaseOrderId,x.VariantId}).IsUnique();e.HasOne<PurchaseOrder>().WithMany(x=>x.Lines).HasForeignKey(x=>new{x.TenantId,Id=x.PurchaseOrderId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<StockItem>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.VariantId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
    }
    private void Guard()
    {this.GuardWrites(scope);if(ChangeTracker.Entries().Any(e=>e.Entity is InventoryMovement or StockOperation or InventoryBatch&&e.State is EntityState.Modified or EntityState.Deleted))throw new BusinessFault("InventoryHistoryImmutable",403);}
    public override int SaveChanges(bool accept){Guard();return base.SaveChanges(accept);}
    public override Task<int> SaveChangesAsync(bool accept,CancellationToken ct=default){Guard();return base.SaveChangesAsync(accept,ct);}
}
