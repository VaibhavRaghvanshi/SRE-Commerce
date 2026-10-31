using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Infrastructure;
public sealed class PricingDbContext(DbContextOptions<PricingDbContext> options,ModuleScope scope):DbContext(options),IModuleStore
{
    public Guid? CurrentTenant=>scope.TenantId;
    public DbContext Context=>this;
    public DbSet<OutboxMessage> Outbox=>Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox=>Set<InboxMessage>();
    public DbSet<AuditEntry> Audit=>Set<AuditEntry>();
    public DbSet<PricingSettings> Settings=>Set<PricingSettings>();
    public DbSet<TaxPolicy> Taxes=>Set<TaxPolicy>();
    public DbSet<PriceList> Lists=>Set<PriceList>();
    public DbSet<ProductPrice> Prices=>Set<ProductPrice>();
    public DbSet<CustomerPriceGroup> Groups=>Set<CustomerPriceGroup>();
    public DbSet<CustomerGroupMember> Members=>Set<CustomerGroupMember>();
    public DbSet<Promotion> Promotions=>Set<Promotion>();
    public DbSet<Coupon> Coupons=>Set<Coupon>();
    public DbSet<PricingQuote> Quotes=>Set<PricingQuote>();
    public DbSet<CouponRedemption> Redemptions=>Set<CouponRedemption>();
    public DbSet<PriceHistory> History=>Set<PriceHistory>();
    public DbSet<PricingImport> Imports=>Set<PricingImport>();
    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();model.Entity<OutboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<InboxMessage>().TenantEntity(x=>x.TenantId==CurrentTenant);model.Entity<AuditEntry>().TenantEntity(x=>x.TenantId==CurrentTenant);
        model.Entity<PricingImport>(e=>{e.ToTable("PricingImports",t=>t.HasCheckConstraint("CK_PricingImports_Result","ISJSON([Result])=1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Source).HasMaxLength(80);e.Property(x=>x.Hash).HasMaxLength(64);e.Property(x=>x.Result).HasMaxLength(5000);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<PricingSettings>(e=>{e.ToTable("PricingSettings",t=>t.HasCheckConstraint("CK_PricingSettings_Precision","[DecimalPlaces] BETWEEN 0 AND 3"));e.HasKey(x=>new{x.TenantId,x.Id});e.HasIndex(x=>x.TenantId).IsUnique();e.Property(x=>x.Currency).HasMaxLength(3);e.Property(x=>x.Version).IsRowVersion();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<TaxPolicy>(e=>{e.ToTable("TaxPolicies",t=>t.HasCheckConstraint("CK_TaxPolicies_Rate","[Rate]>=0 AND [Rate]<=100"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Rate).HasPrecision(7,4);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<CustomerPriceGroup>(e=>{e.ToTable("CustomerPriceGroups");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<CustomerGroupMember>(e=>{e.ToTable("CustomerPriceGroupMembers");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.UserId}).IsUnique();e.HasOne<CustomerPriceGroup>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.GroupId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<PriceList>(e=>{e.ToTable("PriceLists",t=>{t.HasCheckConstraint("CK_PriceLists_Window","[EndsAt]>[StartsAt]");t.HasCheckConstraint("CK_PriceLists_Audience","([Audience]=2 AND [CustomerGroupId] IS NOT NULL) OR ([Audience] IN (0,1) AND [CustomerGroupId] IS NULL)");});e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Currency).HasMaxLength(3);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.HasOne<CustomerPriceGroup>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.CustomerGroupId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<ProductPrice>(e=>{e.ToTable("ProductPrices",t=>t.HasCheckConstraint("CK_ProductPrices_Amounts","[MinimumQuantity]>0 AND [Mrp]>=0 AND [SellingPrice]>=0 AND [SellingPrice]<=[Mrp] AND [CostPrice]>=0"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Sku).HasMaxLength(64);e.Property(x=>x.MinimumQuantity).HasPrecision(18,3);e.Property(x=>x.Mrp).HasPrecision(18,4);e.Property(x=>x.SellingPrice).HasPrecision(18,4);e.Property(x=>x.CostPrice).HasPrecision(18,4);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.PriceListId,x.VariantId,x.MinimumQuantity}).IsUnique();e.HasIndex(x=>new{x.TenantId,x.VariantId,x.Active});e.HasOne<PriceList>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.PriceListId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<TaxPolicy>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.TaxPolicyId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<Promotion>(e=>{e.ToTable("Promotions",t=>{t.HasCheckConstraint("CK_Promotions_Window","[EndsAt]>[StartsAt]");t.HasCheckConstraint("CK_Promotions_Amounts","[Value]>=0 AND [MinimumSpend]>=0 AND ([MaximumDiscount] IS NULL OR [MaximumDiscount]>=0)");t.HasCheckConstraint("CK_Promotions_Targets","ISJSON([VariantIds])=1");});e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Name).HasMaxLength(160);e.Property(x=>x.Value).HasPrecision(18,4);e.Property(x=>x.MinimumSpend).HasPrecision(18,4);e.Property(x=>x.MaximumDiscount).HasPrecision(18,4);e.Property(x=>x.VariantIds).HasMaxLength(5000);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.HasOne<CustomerPriceGroup>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.CustomerGroupId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<Coupon>(e=>{e.ToTable("Coupons",t=>t.HasCheckConstraint("CK_Coupons_Limits","[GlobalLimit]>0 AND [PerCustomerLimit]>=0 AND [PerCustomerLimit]<=[GlobalLimit]"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Code).HasMaxLength(64);e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.Code}).IsUnique();e.HasOne<Promotion>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.PromotionId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<PricingQuote>(e=>{e.ToTable("PricingQuotes",t=>t.HasCheckConstraint("CK_PricingQuotes_Document","ISJSON([Document])=1"));e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.RequestHash).HasMaxLength(64);e.Property(x=>x.GroupContextHash).HasMaxLength(64);e.Property(x=>x.Document).HasMaxLength(131072);e.HasIndex(x=>new{x.TenantId,x.UserId,x.CreatedAt});e.HasIndex(x=>x.ExpiresAt);e.HasOne<Coupon>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.CouponId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<CustomerPriceGroup>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.CustomerGroupId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<CouponRedemption>(e=>{e.ToTable("CouponRedemptions");e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Version).IsRowVersion();e.HasIndex(x=>new{x.TenantId,x.QuoteId}).IsUnique();e.HasIndex(x=>new{x.TenantId,x.CouponId,x.Status,x.ExpiresAt});e.HasIndex(x=>new{x.TenantId,x.CouponId,x.UserId,x.Status});e.HasOne<PricingQuote>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.QuoteId}).OnDelete(DeleteBehavior.Restrict);e.HasOne<Coupon>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.CouponId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
        model.Entity<PriceHistory>(e=>{e.ToTable("PriceHistory",t=>{t.HasCheckConstraint("CK_PriceHistory_Before","ISJSON([Before])=1 OR [Before]=N'null'");t.HasCheckConstraint("CK_PriceHistory_After","ISJSON([After])=1");});e.HasKey(x=>new{x.TenantId,x.Id});e.Property(x=>x.Before).HasMaxLength(10000);e.Property(x=>x.After).HasMaxLength(10000);e.HasIndex(x=>new{x.TenantId,x.PriceId,x.ChangedAt});e.HasOne<ProductPrice>().WithMany().HasForeignKey(x=>new{x.TenantId,Id=x.PriceId}).OnDelete(DeleteBehavior.Restrict);e.TenantEntity(x=>x.TenantId==CurrentTenant);});
    }
    private void Guard()
    {this.GuardWrites(scope);if(ChangeTracker.Entries().Any(e=>e.Entity is PricingQuote or PriceHistory or PricingImport&&e.State is EntityState.Modified or EntityState.Deleted))throw new BusinessFault("PricingHistoryImmutable",403);}
    public override int SaveChanges(bool accept){Guard();return base.SaveChanges(accept);}
    public override Task<int> SaveChangesAsync(bool accept,CancellationToken ct=default){Guard();return base.SaveChangesAsync(accept,ct);}
}
