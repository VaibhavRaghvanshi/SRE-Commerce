using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Infrastructure;
public sealed class StorefrontDbContextFactory:IDesignTimeDbContextFactory<StorefrontDbContext>
{public StorefrontDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<StorefrontDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Storefront"))).Options,new ModuleScope());}
