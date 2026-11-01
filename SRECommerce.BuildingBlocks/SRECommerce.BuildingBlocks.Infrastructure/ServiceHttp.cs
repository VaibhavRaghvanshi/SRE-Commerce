using System.Net.Http.Headers;
using SRECommerce.BuildingBlocks.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace SRECommerce.BuildingBlocks.Infrastructure;

public sealed class ServiceHttp(IServiceTokens tokens,RemoteAccess remote)
{
    public async Task<HttpResponseMessage> Send(string target,HttpRequestMessage request,CancellationToken ct)
    {
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",await tokens.Get(target,ct));
        return await remote.Send(target.ToLowerInvariant(),request,ct);
    }
}

public static class ServiceIdentityRegistration
{
    public static IServiceCollection AddServiceIdentity(this IServiceCollection services)
    {
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);services.AddSingleton<IServiceTokens,ServiceTokens>();services.AddScoped<ServiceHttp>();
        services.AddHttpClient("service-identity",(provider,client)=>
        {
            var development=provider.GetRequiredService<IHostEnvironment>().IsDevelopment();
            client.BaseAddress=RemoteAccess.BaseUri(provider.GetRequiredService<IConfiguration>(),"Identity:BaseUrl",development?"http://localhost:5081/":"https://localhost:7081/",development);client.Timeout=TimeSpan.FromSeconds(6);
        }).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false}).WithDependencyResilience();
        return services;
    }
}
