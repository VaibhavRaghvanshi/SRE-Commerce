using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Payment.Domain;

namespace SRECommerce.Payment.Infrastructure;

public sealed class PaymentDbContext(DbContextOptions<PaymentDbContext> options, ModuleScope scope) : DbContext(options), IModuleStore
{
    public Guid? CurrentTenant => scope.TenantId;
    public DbContext Context => this;
    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();
    public DbSet<InboxMessage> Inbox => Set<InboxMessage>();
    public DbSet<AuditEntry> Audit => Set<AuditEntry>();
    public DbSet<PaymentTransaction> Payments => Set<PaymentTransaction>();
    public DbSet<PaymentAttempt> Attempts => Set<PaymentAttempt>();
    public DbSet<RefundTransaction> Refunds => Set<RefundTransaction>();
    public DbSet<PaymentWebhook> Webhooks => Set<PaymentWebhook>();
    public DbSet<PaymentProviderConfiguration> Configurations => Set<PaymentProviderConfiguration>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ConfigureInfrastructure();
        model.Entity<OutboxMessage>().TenantEntity(x => x.TenantId == CurrentTenant);
        model.Entity<InboxMessage>().TenantEntity(x => x.TenantId == CurrentTenant);
        model.Entity<AuditEntry>().TenantEntity(x => x.TenantId == CurrentTenant);
        void Tenant<T>(string table, Action<EntityTypeBuilder<T>> configure) where T : class, SRECommerce.BuildingBlocks.Kernel.ITenantRecord
        {
            var entity = model.Entity<T>(); entity.ToTable(table); entity.HasKey("TenantId", "Id");
            entity.TenantEntity(x => x.TenantId == CurrentTenant); configure(entity);
        }
        Tenant<PaymentTransaction>("PaymentTransactions", e =>
        {
            e.Property(x => x.Currency).HasMaxLength(3); e.Property(x => x.Method).HasMaxLength(20);
            e.Property(x => x.Provider).HasMaxLength(30); e.Property(x => x.ProviderReference).HasMaxLength(100);
            e.Property(x => x.OrderProof).HasMaxLength(2000); e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.TenantId, x.OrderId }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ProviderReference }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
            e.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt });
            e.HasOne<GiftCard>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.GiftCardId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Payments_Amounts", "[Amount]>=0 AND [Refunded]>=0 AND [Refunded]<=[Amount]");
                t.HasCheckConstraint("CK_Payments_Status", "[Status] BETWEEN 0 AND 7 AND ([Status] IN (5,6) OR [Refunded]=0) AND ([Status]<>6 OR [Refunded]=[Amount]) AND ([Status]<>5 OR ([Refunded]>0 AND [Refunded]<[Amount]))");
                t.HasCheckConstraint("CK_Payments_Proof", "ISJSON([OrderProof])=1");
                t.HasCheckConstraint("CK_Payments_Tender", "([Provider]=N'Sandbox' AND [Method] NOT IN (N'GiftCard',N'CounterCash') AND [GiftCardId] IS NULL) OR ([Provider]=N'GiftCard' AND [Method]=N'GiftCard' AND ([Status] IN (3,4) OR [GiftCardId] IS NOT NULL)) OR ([Provider]=N'Counter' AND [Method]=N'CounterCash' AND [GiftCardId] IS NULL)");
                t.HasCheckConstraint("CK_Payments_Revision", "[Revision]>0");
                t.HasCheckConstraint("CK_Payments_Precision", "[DecimalPlaces] BETWEEN 0 AND 3");
            });
        });
        Tenant<GiftCard>("GiftCards", e =>
        {
            e.Property(x => x.CodeHash).HasMaxLength(64).IsUnicode(false);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Reference).HasMaxLength(300);
            e.Property(x => x.Version).IsRowVersion();
            e.HasIndex(x => new { x.TenantId, x.CodeHash }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.ClaimedBy, x.IssuedAt });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_GiftCards_Balance", "[Value]>0 AND [Balance]>=0 AND [Balance]<=[Value]");
                t.HasCheckConstraint("CK_GiftCards_State", "(([ClaimedBy] IS NULL AND [ClaimedAt] IS NULL) OR ([ClaimedBy] IS NOT NULL AND [ClaimedAt] IS NOT NULL)) AND ([VoidedAt] IS NULL OR ([ClaimedBy] IS NULL AND [Balance]=0))");
            });
        });
        Tenant<PaymentProviderConfiguration>("PaymentProviderConfigurations", e =>
        {
            e.HasIndex(x => x.TenantId).IsUnique(); e.Property(x => x.Version).IsRowVersion();
            e.ToTable(t => t.HasCheckConstraint("CK_PaymentConfiguration_Outcome", "[Outcome] BETWEEN 0 AND 2"));
        });
        Tenant<PaymentAttempt>("PaymentAttempts", e =>
        {
            e.Property(x => x.Action).HasMaxLength(60); e.Property(x => x.ProviderReference).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.PaymentId, x.Sequence }).IsUnique();
            e.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.PaymentId }).OnDelete(DeleteBehavior.Restrict);
        });
        Tenant<RefundTransaction>("RefundTransactions", e =>
        {
            e.Property(x => x.Reason).HasMaxLength(300); e.Property(x => x.ProviderReference).HasMaxLength(100);
            e.HasIndex(x => new { x.TenantId, x.ProviderReference }).IsUnique();
            e.HasIndex(x => new { x.TenantId, x.PaymentId, x.CreatedAt });
            e.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.PaymentId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("CK_Refunds_Amount", "[Amount]>0"));
        });
        Tenant<PaymentWebhook>("PaymentWebhooks", e =>
        {
            e.Property(x => x.PayloadHash).HasMaxLength(64); e.Property(x => x.Outcome).HasMaxLength(20);
            e.HasIndex(x => new { x.TenantId, x.PaymentId, x.ReceivedAt });
            e.HasOne<PaymentTransaction>().WithMany().HasForeignKey(x => new { x.TenantId, Id = x.PaymentId }).OnDelete(DeleteBehavior.Restrict);
        });
        foreach (var entity in model.Model.GetEntityTypes().Where(t => t.ClrType.Namespace == "SRECommerce.Payment.Domain"))
            foreach (var property in entity.GetProperties().Where(p => p.ClrType == typeof(decimal))) { property.SetPrecision(18); property.SetScale(4); }
    }

    private void Guard()
    {
        this.GuardWrites(scope);
        if (ChangeTracker.Entries().Any(e => e.Entity is PaymentAttempt or RefundTransaction or PaymentWebhook && e.State is EntityState.Modified or EntityState.Deleted)) throw new BusinessFault("PaymentHistoryImmutable", 403);
        string[] workflow = [nameof(PaymentTransaction.Status), nameof(PaymentTransaction.Refunded), nameof(PaymentTransaction.ProviderReference), nameof(PaymentTransaction.CancellationRequested), nameof(PaymentTransaction.Revision), nameof(PaymentTransaction.UpdatedAt)];
        if (ChangeTracker.Entries<PaymentTransaction>().Any(e => e.State == EntityState.Modified && e.Properties.Any(p => p.IsModified && !workflow.Contains(p.Metadata.Name)))) throw new BusinessFault("PaymentFactsImmutable", 403);
    }
    public override int SaveChanges(bool accept) { Guard(); return base.SaveChanges(accept); }
    public override Task<int> SaveChangesAsync(bool accept, CancellationToken ct = default) { Guard(); return base.SaveChangesAsync(accept, ct); }
}
