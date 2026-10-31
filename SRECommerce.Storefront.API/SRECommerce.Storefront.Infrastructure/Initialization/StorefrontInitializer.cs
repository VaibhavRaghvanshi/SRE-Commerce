using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.BuildingBlocks.Kernel;
using SRECommerce.Storefront.Application;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Infrastructure;
public sealed class StorefrontInitializer(StorefrontDbContext db):IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        foreach(var type in new[]{"neutral","spices","clothing","grocery","general-store","custom"})if(!await db.Templates.AnyAsync(t=>t.BusinessType==type,ct)){var value=LayoutRules.Default(type);LayoutRules.Validate(value);db.Templates.Add(new(){BusinessType=type,Document=ContractJson.Write(value)});}
        await db.SaveChangesAsync(ct);
    }
}
