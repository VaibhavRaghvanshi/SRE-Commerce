using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace SRECommerce.BuildingBlocks.Infrastructure;
public static class DependencyHttp
{
    // Only safe reads are retried. A timeout never replays a payment/order mutation.
    public static IHttpClientBuilder WithDependencyResilience(this IHttpClientBuilder client)
    {
        client.AddStandardResilienceHandler().Configure((options,provider)=>
        {
            // Explicit local-only opt-in for many cold processes on a small laptop.
            // Never let an accidentally copied review flag alter Production budgets.
            var review=provider.GetService<IHostEnvironment>()?.IsDevelopment()==true
                &&provider.GetService<IConfiguration>()?.GetValue<bool>("LocalReview:DependencyTimeouts")==true;
            options.TotalRequestTimeout.Timeout=TimeSpan.FromSeconds(review?12:5);
            options.AttemptTimeout.Timeout=TimeSpan.FromSeconds(review?8:2);
            options.Retry.MaxRetryAttempts=1;options.Retry.Delay=TimeSpan.FromMilliseconds(100);options.Retry.UseJitter=true;
            options.Retry.DisableForUnsafeHttpMethods();
            options.CircuitBreaker.MinimumThroughput=20;options.CircuitBreaker.SamplingDuration=TimeSpan.FromSeconds(review?30:10);options.CircuitBreaker.BreakDuration=TimeSpan.FromSeconds(5);
        });
        return client;
    }
}
