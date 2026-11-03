using Microsoft.EntityFrameworkCore;using Microsoft.Extensions.DependencyInjection;using Microsoft.Extensions.Hosting;using Microsoft.Extensions.Logging;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Shipping.Application;
namespace SRECommerce.Shipping.Infrastructure;
public sealed class ShippingCallbackWorker(IServiceScopeFactory scopes,TimeProvider clock,ILogger<ShippingCallbackWorker> logger):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken ct)
 {
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(2));while(await timer.WaitForNextTickAsync(ct))
  {
   try
   {
    Guid[] tenants;using(var read=scopes.CreateScope()){var db=read.ServiceProvider.GetRequiredService<ShippingDbContext>();var now=clock.GetUtcNow();tenants=await db.Callbacks.IgnoreQueryFilters().Where(c=>!c.Sent&&!c.ManualReview&&c.NextAttemptAt<=now&&(c.LeaseUntil==null||c.LeaseUntil<=now)).Select(c=>c.TenantId).Distinct().OrderBy(t=>t).Take(10).ToArrayAsync(ct);}
    foreach(var tenant in tenants){try{using var scope=scopes.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenant,"Shipping");await scope.ServiceProvider.GetRequiredService<IShippingCallbacks>().Run(ct);}catch(Exception error)when(error is not OperationCanceledException){logger.LogWarning("Shipping callback deferred ({FailureType})",error.GetType().Name);}}
   }
   catch(Exception error)when(error is not OperationCanceledException){logger.LogWarning("Shipping queue unavailable ({FailureType})",error.GetType().Name);}
  }
 }
}
