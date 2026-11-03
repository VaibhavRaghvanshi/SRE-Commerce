using Microsoft.EntityFrameworkCore;using Microsoft.EntityFrameworkCore.Design;using SRECommerce.BuildingBlocks.Contracts;using SRECommerce.BuildingBlocks.Infrastructure;
namespace SRECommerce.Shipping.Infrastructure;
public sealed class ShippingDbContextFactory:IDesignTimeDbContextFactory<ShippingDbContext>
{public ShippingDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<ShippingDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Shipping"))).Options,new ModuleScope());}
