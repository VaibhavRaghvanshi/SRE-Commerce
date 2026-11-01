using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
namespace SRECommerce.Payment.Infrastructure;
public sealed class PaymentDbContextFactory : IDesignTimeDbContextFactory<PaymentDbContext>
{
    public PaymentDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PaymentDbContext>()
        .UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Payment"))).Options, new ModuleScope());
}
