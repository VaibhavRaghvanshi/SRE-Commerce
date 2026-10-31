using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Cart.Domain;
namespace SRECommerce.Cart.Infrastructure;
public sealed class CartDbContext(DbContextOptions<CartDbContext> options,ModuleScope scope):DbContext(options),IModuleStore
{
 public Guid? CurrentTenant=>scope.TenantId;public DbContext Context=>this;
 public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>();public DbSet<InboxMessage> Inbox=>Set<InboxMessage>();public DbSet<AuditEntry> Audit=>Set<AuditEntry>();
 public DbSet<ShoppingCart> Carts=>Set<ShoppingCart>();public DbSet<CartItem> Items=>Set<CartItem>();public DbSet<CartSession> Sessions=>Set<CartSession>();public DbSet<CartOperation> Operations=>Set<CartOperation>();public DbSet<CheckoutPreparation> Preparations=>Set<CheckoutPreparation>();
 protected override void OnModelCreating(ModelBuilder m)
 {
  m.ConfigureInfrastructure();m.Entity<OutboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);m.Entity<InboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);m.Entity<AuditEntry>().TenantEntity(x=>x.TenantId==CurrentTenant);
  m.Entity<ShoppingCart>(e=>{e.ToTable("Carts",t=>{t.HasCheckConstraint("CK_Carts_Revision","[Revision]>=0");t.HasCheckConstraint("CK_Carts_State","[Status] BETWEEN 0 AND 3");});e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.UserId}).IsUnique().HasFilter("[UserId] IS NOT NULL AND [Status]=0");e.HasIndex(x=>new{x.TenantId,x.ExpiresAt});e.TenantEntity(x=>x.TenantId==CurrentTenant);});
  m.Entity<CartItem>(e=>{e.ToTable("CartItems",t=>t.HasCheckConstraint("CK_CartItems_Quantity","[Quantity]>0 AND [Quantity]<=1000"));e.HasKey(x=>new{x.TenantId,x.CartId,x.VariantId});e.Property(x=>x.Quantity).HasPrecision(18,3);e.Property(x=>x.Sku).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Slug).HasMaxLength(120);e.HasOne<ShoppingCart>().WithMany(x=>x.Items).HasForeignKey(x=>new{x.TenantId,Id=x.CartId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
  m.Entity<CartSession>(e=>{e.ToTable("CartSessions");e.HasKey(x=>new{x.TenantId,x.CartId});e.Property(x=>x.CapabilityHash).HasMaxLength(32).IsFixedLength();e.HasOne<ShoppingCart>().WithOne().HasForeignKey<CartSession>(x=>new{x.TenantId,Id=x.CartId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
  m.Entity<CartOperation>(e=>{e.ToTable("CartOperations",t=>t.HasCheckConstraint("CK_CartOperations_Result","ISJSON([Result])=1"));e.HasKey(x=>new{x.TenantId,x.CartId,x.Id});e.Property(x=>x.Hash).HasMaxLength(64);e.Property(x=>x.Result).HasMaxLength(131072);e.HasOne<ShoppingCart>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.CartId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
  m.Entity<CheckoutPreparation>(e=>{e.ToTable("CheckoutPreparations",t=>t.HasCheckConstraint("CK_CheckoutPreparations_Document","ISJSON([Document])=1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Hash).HasMaxLength(64);e.Property(x=>x.Document).HasMaxLength(131072);e.HasIndex(x=>new{x.TenantId,x.CartId,x.CartRevision});e.HasOne<ShoppingCart>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.CartId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
 }
 private void Guard(){this.GuardWrites(scope);if(ChangeTracker.Entries().Any(e=>e.Entity is CartOperation or CheckoutPreparation&&e.State is EntityState.Modified or EntityState.Deleted))throw new BusinessFault("CartHistoryImmutable",403);}
 public override int SaveChanges(bool accept){Guard();return base.SaveChanges(accept);}public override Task<int> SaveChangesAsync(bool accept,CancellationToken ct=default){Guard();return base.SaveChangesAsync(accept,ct);}
}
