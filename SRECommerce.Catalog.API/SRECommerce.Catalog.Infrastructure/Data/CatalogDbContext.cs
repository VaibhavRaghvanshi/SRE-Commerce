using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Catalog.Domain;
namespace SRECommerce.Catalog.Infrastructure;
public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options,ModuleScope scope):DbContext(options),IModuleStore
{
    public Guid? CurrentTenant=>scope.TenantId;
    public DbContext Context=>this;
    public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox=>Set<InboxMessage>();
    public DbSet<AuditEntry> Audit=>Set<AuditEntry>();
    public DbSet<Category> Categories=>Set<Category>();
    public DbSet<Brand> Brands=>Set<Brand>();
    public DbSet<AttributeDefinition> Attributes=>Set<AttributeDefinition>();
    public DbSet<CategoryAttribute> Bindings=>Set<CategoryAttribute>();
    public DbSet<Product> Products=>Set<Product>();
    public DbSet<ProductVariant> Variants=>Set<ProductVariant>();
    public DbSet<ProductPublication> Publications=>Set<ProductPublication>();
    public DbSet<ProductCollection> Collections=>Set<ProductCollection>();
    public DbSet<CatalogImport> Imports=>Set<CatalogImport>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();model.Entity<OutboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<InboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<AuditEntry>().TenantEntity(x=>x.TenantId==CurrentTenant);
        model.Entity<Category>(e=>{e.ToTable("Categories");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Slug).HasMaxLength(120);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Slug}).IsUnique();e.HasOne<Category>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ParentId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<Brand>(e=>{e.ToTable("Brands");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Slug).HasMaxLength(120);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Slug}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<AttributeDefinition>(e=>{e.ToTable("AttributeDefinitions",t=>t.HasCheckConstraint("CK_AttributeDefinitions_Options","ISJSON([Options])=1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(120);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Unit).HasMaxLength(30);e.Property(x=>x.Options).HasMaxLength(10000);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<CategoryAttribute>(e=>{e.ToTable("CategoryAttributeMappings");e.HasKey(x=>new{x.TenantId,x.CategoryId,x.AttributeId});e.HasOne<Category>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CategoryId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<AttributeDefinition>().WithMany().HasForeignKey(x=>new{x.TenantId,x.AttributeId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<Product>(e=>{e.ToTable("Products",t=>{t.HasCheckConstraint("CK_Products_Attributes","ISJSON([Attributes])=1");t.HasCheckConstraint("CK_Products_Specifications","ISJSON([Specifications])=1");});e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Slug).HasMaxLength(120);e.Property(x=>x.Description).HasMaxLength(10000);e.Property(x=>x.SeoTitle).HasMaxLength(160);e.Property(x=>x.SeoDescription).HasMaxLength(320);e.Property(x=>x.Attributes).HasMaxLength(40000);e.Property(x=>x.Specifications).HasMaxLength(65536);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Slug}).IsUnique();e.HasIndex(x=>new{x.TenantId,x.CategoryId,x.Status});e.HasOne<Category>().WithMany().HasForeignKey(x=>new{x.TenantId,x.CategoryId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<Brand>().WithMany().HasForeignKey(x=>new{x.TenantId,x.BrandId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ProductVariant>(e=>{e.ToTable("ProductVariants",t=>t.HasCheckConstraint("CK_ProductVariants_Options","ISJSON([Options])=1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Sku).HasMaxLength(64);e.Property(x=>x.Barcode).HasMaxLength(64);e.Property(x=>x.Options).HasMaxLength(4000);e.HasIndex(x=>new{x.TenantId,x.Sku}).IsUnique();e.HasIndex(x=>new{x.TenantId,x.Barcode}).IsUnique().HasFilter("[Barcode] IS NOT NULL");e.HasOne<Product>().WithMany(x=>x.Variants).HasForeignKey(x=>new{x.TenantId,x.ProductId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ProductMedia>(e=>{e.ToTable("ProductMedia");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Url).HasMaxLength(1024);e.Property(x=>x.Alt).HasMaxLength(200);e.HasOne<Product>().WithMany(x=>x.Media).HasForeignKey(x=>new{x.TenantId,x.ProductId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ProductBundleItem>(e=>{e.ToTable("ProductBundles",t=>t.HasCheckConstraint("CK_ProductBundles_Quantity","[Quantity]>0"));e.HasKey(x=>new{x.TenantId,x.ProductId,x.ComponentVariantId});e.Property(x=>x.Quantity).HasPrecision(18,3);e.HasOne<Product>().WithMany(x=>x.BundleItems).HasForeignKey(x=>new{x.TenantId,x.ProductId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<ProductVariant>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ComponentVariantId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ProductRelationship>(e=>{e.ToTable("ProductRelationships");e.HasKey(x=>new{x.TenantId,x.ProductId,x.RelatedProductId});e.Property(x=>x.Kind).HasMaxLength(30);e.HasOne<Product>().WithMany(x=>x.Relationships).HasForeignKey(x=>new{x.TenantId,x.ProductId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<Product>().WithMany().HasForeignKey(x=>new{x.TenantId,x.RelatedProductId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ProductPublication>(e=>{e.ToTable("ProductPublications",t=>t.HasCheckConstraint("CK_ProductPublications_Document","ISJSON([Document])=1"));e.HasKey(x=>new{x.TenantId,x.ProductId,x.Revision});e.Property(x=>x.Document).HasMaxLength(131072);e.Property(x=>x.Hash).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Slug).HasMaxLength(120);e.HasIndex(x=>new{x.TenantId,x.Slug});e.HasOne<Product>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ProductId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ProductCollection>(e=>{e.ToTable("ProductCollections");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Slug).HasMaxLength(120);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Slug}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<CollectionProduct>(e=>{e.ToTable("CollectionProducts");e.HasKey(x=>new{x.TenantId,x.CollectionId,x.ProductId});e.HasOne<ProductCollection>().WithMany(x=>x.Products).HasForeignKey(x=>new{x.TenantId,x.CollectionId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<Product>().WithMany().HasForeignKey(x=>new{x.TenantId,x.ProductId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<CatalogImport>(e=>{e.ToTable("CatalogImports");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Source).HasMaxLength(80);e.Property(x=>x.Hash).HasMaxLength(64);e.Property(x=>x.Result).HasMaxLength(5000);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
    }
    private void Guard()
    {this.GuardWrites(scope);if(ChangeTracker.Entries().Any(e=>e.Entity is ProductPublication or CatalogImport&&e.State is EntityState.Modified or EntityState.Deleted))throw new BusinessFault("CatalogHistoryImmutable",403);}
    public override int SaveChanges(bool acceptAllChangesOnSuccess){Guard();return base.SaveChanges(acceptAllChangesOnSuccess);}
    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess,CancellationToken ct=default){Guard();return base.SaveChangesAsync(acceptAllChangesOnSuccess,ct);}
}
