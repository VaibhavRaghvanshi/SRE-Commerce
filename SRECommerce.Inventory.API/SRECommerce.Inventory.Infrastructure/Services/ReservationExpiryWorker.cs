using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Inventory.Application;
using SRECommerce.Inventory.Domain;
namespace SRECommerce.Inventory.Infrastructure;
public sealed class ReservationExpiryWorker(IServiceScopeFactory scopes,TimeProvider clock,ILogger<ReservationExpiryWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromSeconds(30));
        while(await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                Guid[] tenants;
                using(var discover=scopes.CreateScope())
                {var db=discover.ServiceProvider.GetRequiredService<InventoryDbContext>();var now=clock.GetUtcNow();tenants=await db.Reservations.IgnoreQueryFilters().Where(r=>r.Status==ReservationStatus.Active&&r.ExpiresAt<=now).OrderBy(r=>r.ExpiresAt).Select(r=>r.TenantId).Take(100).Distinct().ToArrayAsync(stoppingToken);}
                foreach(var tenant in tenants)
                {
                    try
                    {
                        using var scope=scopes.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenant,"Inventory");
                        await scope.ServiceProvider.GetRequiredService<IInventoryMaintenance>().ExpireBound(stoppingToken);
                    }
                    catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
                    catch(Exception error){logger.LogWarning("One tenant's reservation expiry deferred after {FailureType}; remaining tenants continue",error.GetType().Name);}
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){return;}
            catch(Exception error){logger.LogWarning("Reservation expiry deferred after {FailureType}; next pass will retry",error.GetType().Name);}
        }
    }
}
