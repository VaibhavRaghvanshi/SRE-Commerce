using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Infrastructure;
public sealed class ThemeDbContextFactory:IDesignTimeDbContextFactory<ThemeDbContext>
{public ThemeDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<ThemeDbContext>().UseSqlServer(ModuleDatabase.DesignConnection(ServiceManifest.Get("Theme"))).Options,new ModuleScope());}
