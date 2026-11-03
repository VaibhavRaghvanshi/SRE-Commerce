using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Shipping.Domain;
namespace SRECommerce.Shipping.Infrastructure;
public sealed class ShippingDbContext(DbContextOptions<ShippingDbContext> options,ModuleScope scope):DbContext(options),IModuleStore
{
 public Guid? CurrentTenant=>scope.TenantId;public DbContext Context=>this;public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>();public DbSet<InboxMessage> Inbox=>Set<InboxMessage>();public DbSet<AuditEntry> Audit=>Set<AuditEntry>();
 public DbSet<ShippingZone> Zones=>Set<ShippingZone>();public DbSet<ShippingMethod> Methods=>Set<ShippingMethod>();public DbSet<ShippingQuote> Quotes=>Set<ShippingQuote>();public DbSet<Shipment> Shipments=>Set<Shipment>();public DbSet<ShipmentItem> Items=>Set<ShipmentItem>();
 public DbSet<TrackingEvent> Tracking=>Set<TrackingEvent>();public DbSet<ShipmentCallback> Callbacks=>Set<ShipmentCallback>();public DbSet<ReturnPickup> Pickups=>Set<ReturnPickup>();public DbSet<ShippingOperation> Operations=>Set<ShippingOperation>();
 protected override void OnModelCreating(ModelBuilder model)
 {
  model.ConfigureInfrastructure();model.Entity<OutboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<InboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<AuditEntry>().TenantEntity(x=>x.TenantId==CurrentTenant);
  void Tenant<T>(string table,Action<EntityTypeBuilder<T>> configure)where T:class,SRECommerce.BuildingBlocks.Kernel.ITenantRecord
  {var entity=model.Entity<T>();entity.ToTable(table);entity.HasKey("TenantId","Id");entity.TenantEntity(x=>x.TenantId==CurrentTenant);configure(entity);}
  Tenant<ShippingZone>("ShippingZones",e=>{e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Countries).HasMaxLength(4096);e.Property(x=>x.PostalPrefixes).HasMaxLength(16384);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.ToTable(t=>t.HasCheckConstraint("CK_ShippingZones_Json","ISJSON([Countries])=1 AND ISJSON([PostalPrefixes])=1"));});
  Tenant<ShippingMethod>("ShippingMethods",e=>
  {
   e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Description).HasMaxLength(500);e.Property(x=>x.Currency).HasMaxLength(3);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();
   e.HasOne<ShippingZone>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.ZoneId}).OnDelete(DeleteBehavior.Restrict);
   e.ToTable(t=>{t.HasCheckConstraint("CK_ShippingMethods_Amounts","[FlatFee]>=0 AND [PerUnitFee]>=0 AND [TaxRate]>=0 AND [TaxRate]<=100 AND [MinimumOrder]>=0 AND ([MaximumOrder] IS NULL OR [MaximumOrder]>=[MinimumOrder]) AND ([FreeAbove] IS NULL OR [FreeAbove]>=0) AND [MaximumUnits]>0 AND [MaximumUnits]<=50000");t.HasCheckConstraint("CK_ShippingMethods_Pickup","[Kind] BETWEEN 0 AND 2 AND (([Kind]=1 AND [PickupWarehouseId] IS NOT NULL) OR ([Kind]<>1 AND [PickupWarehouseId] IS NULL))");});
  });
  Tenant<ShippingQuote>("ShippingQuotes",e=>{e.Property(x=>x.Document).HasMaxLength(32768);e.Property(x=>x.ConfigurationSnapshot).HasMaxLength(32768);e.HasIndex(x=>new{x.TenantId,x.PreparationId});e.HasOne<ShippingMethod>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.MethodId}).OnDelete(DeleteBehavior.Restrict);e.ToTable(t=>t.HasCheckConstraint("CK_ShippingQuotes_Json","ISJSON([Document])=1 AND ISJSON([ConfigurationSnapshot])=1"));});
  Tenant<Shipment>("Shipments",e=>
  {
   e.Property(x=>x.Provider).HasMaxLength(30);e.Property(x=>x.CourierReference).HasMaxLength(100);e.Property(x=>x.ContactDocument).HasMaxLength(4000);e.Property(x=>x.ProofHash).HasMaxLength(64);e.Property(x=>x.Version).IsRowVersion();
   e.HasIndex(x=>new{x.TenantId,x.OrderId});e.HasIndex(x=>new{x.TenantId,x.Status,x.CreatedAt});e.HasIndex(x=>new{x.TenantId,x.CourierReference}).IsUnique();e.HasOne<ShippingQuote>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.QuoteId}).OnDelete(DeleteBehavior.Restrict);
   e.ToTable(t=>{t.HasCheckConstraint("CK_Shipments_Contact","ISJSON([ContactDocument])=1");t.HasCheckConstraint("CK_Shipments_State","[Status] BETWEEN 0 AND 5 AND [Kind] BETWEEN 0 AND 2 AND [Revision]>0");t.HasCheckConstraint("CK_Shipments_Provider","[Provider] IN (N'Local',N'SandboxCourier')");});
  });
  model.Entity<ShipmentItem>(e=>{e.ToTable("ShipmentItems",t=>t.HasCheckConstraint("CK_ShipmentItems_Quantity","[Quantity]>0 AND [Quantity]<=1000"));e.HasKey(x=>new{x.TenantId,x.ShipmentId,x.VariantId});e.TenantEntity(x=>x.TenantId==CurrentTenant);e.HasOne<Shipment>().WithMany(s=>s.Items).HasForeignKey(x=>new{x.TenantId,Id=x.ShipmentId}).OnDelete(DeleteBehavior.Restrict);});
  Tenant<TrackingEvent>("TrackingEvents",e=>{e.Property(x=>x.Note).HasMaxLength(300);e.HasIndex(x=>new{x.TenantId,x.ShipmentId,x.Sequence}).IsUnique();e.HasOne<Shipment>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.ShipmentId}).OnDelete(DeleteBehavior.Restrict);});
  Tenant<ShipmentCallback>("ShipmentCallbacks",e=>{e.Property(x=>x.RequestDocument).HasMaxLength(2000);e.Property(x=>x.LastFault).HasMaxLength(80);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.Sent,x.ManualReview,x.NextAttemptAt,x.LeaseUntil});e.HasIndex(x=>new{x.TenantId,x.ShipmentId,x.Sequence}).IsUnique();e.HasOne<Shipment>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.ShipmentId}).OnDelete(DeleteBehavior.Restrict);e.ToTable(t=>{t.HasCheckConstraint("CK_ShipmentCallbacks_Request","[RequestDocument] IS NULL OR ISJSON([RequestDocument])=1");t.HasCheckConstraint("CK_ShipmentCallbacks_State","[Attempts]>=0 AND [TargetStatus] IN (2,3,4)");});});
  Tenant<ReturnPickup>("ReturnPickups",e=>{e.Property(x=>x.ContactDocument).HasMaxLength(4000);e.Property(x=>x.Items).HasMaxLength(10000);e.Property(x=>x.Note).HasMaxLength(300);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.ReturnId}).IsUnique().HasFilter("[Status]<>2");e.HasIndex(x=>new{x.TenantId,x.CreatedAt});e.ToTable(t=>{t.HasCheckConstraint("CK_ReturnPickups_Window","[WindowTo]>[WindowFrom] AND [Status] BETWEEN 0 AND 2");t.HasCheckConstraint("CK_ReturnPickups_Json","ISJSON([ContactDocument])=1 AND ISJSON([Items])=1");});});
  Tenant<ShippingOperation>("ShippingOperations",e=>{e.Property(x=>x.Action).HasMaxLength(80);e.Property(x=>x.Hash).HasMaxLength(64);e.Property(x=>x.Result).HasMaxLength(131072);e.ToTable(t=>t.HasCheckConstraint("CK_ShippingOperations_Result","ISJSON([Result])=1"));});
  foreach(var entity in model.Model.GetEntityTypes().Where(t=>t.ClrType.Namespace=="SRECommerce.Shipping.Domain"))foreach(var property in entity.GetProperties().Where(p=>p.ClrType==typeof(decimal)||p.ClrType==typeof(decimal?))){property.SetPrecision(18);property.SetScale(property.Name is "Quantity" or "MaximumUnits"?3:4);}
 }
 private void Guard()
 {
  this.GuardWrites(scope);
  if(ChangeTracker.Entries().Any(e=>e.Entity is ShippingQuote or ShippingOperation or TrackingEvent or ShipmentItem&&e.State is EntityState.Modified or EntityState.Deleted))throw new BusinessFault("ShippingHistoryImmutable",403);
  string[] mutable=[nameof(Shipment.Status),nameof(Shipment.Revision),nameof(Shipment.UpdatedAt),nameof(Shipment.DispatchedAt),nameof(Shipment.DeliveredAt)];
  if(ChangeTracker.Entries<Shipment>().Any(e=>e.State==EntityState.Modified&&e.Properties.Any(p=>p.IsModified&&!mutable.Contains(p.Metadata.Name))))throw new BusinessFault("ShipmentSnapshotImmutable",403);
  if(ChangeTracker.Entries<ReturnPickup>().Any(e=>e.State==EntityState.Modified&&e.Properties.Any(p=>p.IsModified&&p.Metadata.Name is not(nameof(ReturnPickup.Status) or nameof(ReturnPickup.Note)))))throw new BusinessFault("PickupSnapshotImmutable",403);
  foreach(var entry in ChangeTracker.Entries<ShipmentCallback>().Where(e=>e.State==EntityState.Modified))
   if(entry.OriginalValues.GetValue<string?>(nameof(ShipmentCallback.RequestDocument)) is {} original&&entry.Entity.RequestDocument!=original)throw new BusinessFault("ShipmentCallbackRequestImmutable",403);
 }
 public override int SaveChanges(bool accept){Guard();return base.SaveChanges(accept);}public override Task<int> SaveChangesAsync(bool accept,CancellationToken ct=default){Guard();return base.SaveChangesAsync(accept,ct);}
}
