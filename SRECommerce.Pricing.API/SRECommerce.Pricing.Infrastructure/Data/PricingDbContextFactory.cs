using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
namespace SRECommerce.Pricing.Infrastructure;
public sealed class PricingDbContextFactory:IDesignTimeDbContextFactory<PricingDbContext>
{
    public PricingDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<PricingDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Pricing"))).Options,new ModuleScope());
}
