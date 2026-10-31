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
using SRECommerce.Theme.Application;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Infrastructure;
public sealed class ThemeInitializer(ThemeDbContext db):IModuleInitializer
{
    public async Task InitializeAsync(CancellationToken ct)
    {
        foreach(var type in new[]{"neutral","spices","clothing","grocery","general-store","custom"})
            if(!await db.Presets.AnyAsync(p=>p.BusinessType==type,ct)){var document=ThemeRules.Default(type);ThemeRules.Validate(document);db.Presets.Add(new ThemePreset{BusinessType=type,Document=ContractJson.Write(document)});}
        await db.SaveChangesAsync(ct);
    }
}
