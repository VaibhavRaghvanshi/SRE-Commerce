using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Entities;

namespace SRECommerce.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Banner> Banners => Set<Banner>();
        public DbSet<HomeSection> HomeSections => Set<HomeSection>();

        public DbSet<HomeSectionItem> HomeSectionItems => Set<HomeSectionItem>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // agar Category/Product ke configs hain to yahi rakho
            modelBuilder.Entity<HomeSection>()
    .HasMany(x => x.Items)
    .WithOne(x => x.HomeSection)
    .HasForeignKey(x => x.HomeSectionId)
    .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<HomeSection>()
                .Property(x => x.Type)
                .HasConversion<int>();

            modelBuilder.Entity<HomeSectionItem>()
                .Property(x => x.EntityType)
                .HasConversion<int>();

            modelBuilder.Entity<Banner>()
                .Property(x => x.LinkType)
                .HasConversion<int>();

            modelBuilder.Entity<Product>()
                .Property(x => x.Price)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Product>()
                .Property(x => x.Discount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .HasMany(x => x.Items)
                .WithOne(x => x.Order)
                .HasForeignKey(x => x.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<Order>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Order>()
                .Property(x => x.Subtotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(x => x.DiscountTotal)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(x => x.ShippingFee)
                .HasPrecision(18, 2);

            modelBuilder.Entity<Order>()
                .Property(x => x.Total)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(x => x.UnitPrice)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(x => x.Discount)
                .HasPrecision(18, 2);

            modelBuilder.Entity<OrderItem>()
                .Property(x => x.LineTotal)
                .HasPrecision(18, 2);

        }
    }
}

