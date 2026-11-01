using Microsoft.EntityFrameworkCore;using Microsoft.Extensions.DependencyInjection;using Microsoft.Extensions.Hosting;using Microsoft.Extensions.Logging;using SRECommerce.BuildingBlocks.Infrastructure;using SRECommerce.Order.Application;using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Infrastructure;
public sealed class OrderRecoveryWorker(IServiceScopeFactory scopes,ILogger<OrderRecoveryWorker> logger,TimeProvider clock):BackgroundService
{
 protected override async Task ExecuteAsync(CancellationToken stoppingToken)
 {
  using var timer=new PeriodicTimer(TimeSpan.FromSeconds(2));
  while(await timer.WaitForNextTickAsync(stoppingToken))
  {
   Guid[] tenants;
   try
   {
    using var read=scopes.CreateScope();var db=read.ServiceProvider.GetRequiredService<OrderDbContext>();var now=clock.GetUtcNow();
    var checkout=db.Intents.IgnoreQueryFilters().Where(i=>(i.State==CheckoutState.AwaitingProof||i.State==CheckoutState.Processing||i.State==CheckoutState.Compensating)&&i.NextAttemptAt<=now&&(i.LeaseUntil==null||i.LeaseUntil<=now)).Select(i=>i.TenantId);
    var shipments=db.Fulfillments.IgnoreQueryFilters().Where(f=>f.Status==FulfillmentStatus.Requested&&f.NextAttemptAt<=now).Select(f=>f.TenantId);
    var returns=db.Returns.IgnoreQueryFilters().Where(r=>(r.Status==ReturnStatus.Receiving||r.Status==ReturnStatus.RefundPending)&&r.NextAttemptAt<=now).Select(r=>r.TenantId);
    var cod=db.Orders.IgnoreQueryFilters().Where(o=>o.Status==OrderStatus.Delivered&&o.PaymentState==PaymentState.CashOnDelivery).Select(o=>o.TenantId);
    tenants=await checkout.Union(shipments).Union(returns).Union(cod).OrderBy(t=>t).Take(10).ToArrayAsync(stoppingToken);
   }
   catch(Exception error)when(error is not OperationCanceledException){logger.LogWarning("Order queue unavailable ({FailureType})",error.GetType().Name);continue;}
   foreach(var tenant in tenants)
   {
    try{using var scope=scopes.CreateScope();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(tenant,"Order");await scope.ServiceProvider.GetRequiredService<IOrderSaga>().Recover(stoppingToken);await scope.ServiceProvider.GetRequiredService<IOrderWorkflows>().Run(stoppingToken);}
    catch(Exception error)when(error is not OperationCanceledException){logger.LogWarning("Order recovery deferred ({FailureType})",error.GetType().Name);}
   }
  }
 }
}
