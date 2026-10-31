using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? LegacyPasswordHash { get; set; }
}

public sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options, ITenantContext tenant) : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public Guid? CurrentTenant => tenant.TenantId;
    public DbSet<UserProfile> Profiles => Set<UserProfile>();
    public DbSet<TenantMembership> Memberships => Set<TenantMembership>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<UserSession> Sessions => Set<UserSession>();
    public DbSet<SecurityAuditLog> AuditLogs => Set<SecurityAuditLog>();
    public DbSet<EmailOutbox> Emails => Set<EmailOutbox>();
    public DbSet<LegacyUserMapping> LegacyUsers => Set<LegacyUserMapping>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        base.OnModelCreating(model);
        model.UseOpenIddict();
        // Subjects are GUID user IDs or bounded svc:<service> identifiers. Bound
        // these indexed columns to keep SQL Server composite keys below 1700 bytes.
        model.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreAuthorization>().Property(x=>x.Subject).HasMaxLength(36);
        model.Entity<OpenIddict.EntityFrameworkCore.Models.OpenIddictEntityFrameworkCoreToken>().Property(x=>x.Subject).HasMaxLength(36);
        model.Entity<ApplicationUser>().ToTable("Users");
        model.Entity<ApplicationUser>().Property(u => u.LegacyPasswordHash).HasMaxLength(200);
        model.Entity<ApplicationUser>().HasIndex(u => u.NormalizedEmail).IsUnique().HasFilter("[NormalizedEmail] IS NOT NULL");
        model.Entity<IdentityRole<Guid>>().ToTable("Roles");
        model.Entity<IdentityUserRole<Guid>>().ToTable("UserRoles");
        model.Entity<IdentityUserLogin<Guid>>().Property(x=>x.LoginProvider).HasMaxLength(128);
        model.Entity<IdentityUserLogin<Guid>>().Property(x=>x.ProviderKey).HasMaxLength(128);
        model.Entity<IdentityUserToken<Guid>>().Property(x=>x.LoginProvider).HasMaxLength(128);
        model.Entity<IdentityUserToken<Guid>>().Property(x=>x.Name).HasMaxLength(128);
        model.Entity<UserProfile>(e => { e.ToTable("UserProfiles"); e.HasKey(x => x.UserId); e.Property(x => x.FullName).HasMaxLength(160); e.HasOne<ApplicationUser>().WithOne().HasForeignKey<UserProfile>(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<TenantMembership>(e => { e.ToTable("TenantMemberships"); e.HasKey(x => x.Id); e.Property(x => x.Role).HasMaxLength(40); e.Property(x => x.Version).IsRowVersion(); e.HasIndex(x => new { x.TenantId, x.UserId }).IsUnique(); e.HasQueryFilter(x => CurrentTenant != null && x.TenantId == CurrentTenant); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<RolePermission>(e => { e.ToTable("RolePermissions"); e.HasKey(x => x.Id); e.Property(x => x.Role).HasMaxLength(40); e.Property(x => x.Permission).HasMaxLength(100); e.HasIndex(x => new { x.TenantId, x.Role, x.Permission }).IsUnique(); e.HasQueryFilter(x => CurrentTenant != null && x.TenantId == CurrentTenant); });
        model.Entity<UserSession>(e => { e.ToTable("UserSessions"); e.HasKey(x => x.Id); e.Property(x => x.AuthorizationId).HasMaxLength(100); e.Property(x => x.SecurityStamp).HasMaxLength(100); e.Property(x => x.Version).IsRowVersion(); e.HasIndex(x => new { x.UserId, x.RevokedAt, x.ExpiresAt }); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
        model.Entity<SecurityAuditLog>(e => { e.ToTable("SecurityAuditLogs"); e.HasKey(x => x.Id); e.Property(x => x.Action).HasMaxLength(100); e.Property(x => x.Outcome).HasMaxLength(100); e.HasIndex(x => new { x.UserId, x.CreatedAt }); e.HasIndex(x => new { x.TenantId, x.CreatedAt }); });
        model.Entity<EmailOutbox>(e => { e.ToTable("EmailOutbox"); e.HasKey(x => x.Id); e.Property(x => x.Recipient).HasMaxLength(254); e.Property(x => x.Subject).HasMaxLength(160); e.HasIndex(x => new { x.DeliveredAt, x.NextAttemptAt }); });
        model.Entity<LegacyUserMapping>(e => { e.ToTable("LegacyUserMappings"); e.HasKey(x => x.Id); e.Property(x => x.Source).HasMaxLength(40); e.Property(x => x.LegacyRole).HasMaxLength(40); e.HasIndex(x => new { x.Source, x.LegacyId }).IsUnique(); e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict); });
    }
}

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("IDENTITY_MIGRATION_CONNECTION")
            ?? "Server=localhost,1433;Database=SRECommerce_IdentityDB;Integrated Security=true;Encrypt=true;TrustServerCertificate=true";
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseSqlServer(connection).UseOpenIddict().Options, new NoTenantContext());
    }
}
