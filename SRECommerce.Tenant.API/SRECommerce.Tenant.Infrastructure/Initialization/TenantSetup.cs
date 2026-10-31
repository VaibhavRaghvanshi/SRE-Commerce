using Microsoft.EntityFrameworkCore;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;

public static class TenantSetup
{
    public static async Task Initialize(TenantDbContext db)
    {
        var database=db.Database.GetDbConnection().Database;
        if(database!="SRECommerce_TenantDB" && !database.StartsWith("SRECommerce_TenantTests_",StringComparison.Ordinal)) throw new InvalidOperationException("Wrong initialization database.");
        await using var transaction=await db.Database.BeginTransactionAsync();
        foreach(var (code,name) in new[]{("spices","Spices and masala"),("clothing","Clothing and fashion"),("grocery","Grocery"),("general-store","General store"),("custom","Custom business")})
            if(!await db.BusinessTypes.AnyAsync(t=>t.Code==code)) db.BusinessTypes.Add(new BusinessType{Code=code,Name=name});
        foreach(var plan in new[]{new SubscriptionPlan{Code="starter",Name="Development starter",MaxLocations=1,MaxDomains=1,MaxUsers=5},new SubscriptionPlan{Code="growth",Name="Development growth",MaxLocations=5,MaxDomains=3,MaxUsers=50}})
        {
            if(await db.Plans.AnyAsync(p=>p.Code==plan.Code)) continue;
            db.Plans.Add(plan);
            foreach(var module in TenantRules.Modules.Where(m=>plan.Code=="growth" || m is "catalog" or "storefront" or "cart" or "orders")) db.Features.Add(new PlanFeature{PlanCode=plan.Code,Feature=module,Enabled=true});
        }
        await db.SaveChangesAsync();await transaction.CommitAsync();Console.WriteLine("Tenant business-type/development-plan catalog initialized idempotently. No businesses or subscriptions were seeded.");
    }
}
