using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace SRECommerce.BuildingBlocks.Infrastructure;
public static class DependencyHttp
{
    // Only safe reads are retried. A timeout never replays a payment/order mutation.
    public static IHttpClientBuilder WithDependencyResilience(this IHttpClientBuilder client)
    {
        client.AddStandardResilienceHandler(options=>
        {
            options.TotalRequestTimeout.Timeout=TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout=TimeSpan.FromSeconds(2);
            options.Retry.MaxRetryAttempts=1;options.Retry.Delay=TimeSpan.FromMilliseconds(100);options.Retry.UseJitter=true;
            options.Retry.DisableForUnsafeHttpMethods();
            options.CircuitBreaker.MinimumThroughput=20;options.CircuitBreaker.SamplingDuration=TimeSpan.FromSeconds(10);options.CircuitBreaker.BreakDuration=TimeSpan.FromSeconds(5);
        });
        return client;
    }
}
