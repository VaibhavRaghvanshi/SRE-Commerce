using System.Net;
using System.Net.Http.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Discovery.Application;

namespace SRECommerce.Discovery.Infrastructure;

public sealed class DiscoveryAccess(ModuleAccess access, ModuleScope scope) : IDiscoveryAccess
{
    public Task Public(Guid tenant, CancellationToken ct) => access.PublicAsync(tenant, ct);
    public async Task<Guid> Human(Guid tenant, ActorPermission role, CancellationToken ct)
    { await access.AuthorizeAsync(tenant, role, ct); return scope.User!.UserId; }
}

public sealed class DiscoverySources(RemoteAccess remote, ServiceHttp machine) : IDiscoverySources
{
    public async Task<IndexPage> CatalogPage(Guid tenant, int page, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/public/tenants/{tenant}/catalog/products?page={page}&pageSize=50");
        using var response = await remote.Send("catalog", request, ct);
        if (!response.IsSuccessStatusCode) throw new DependencyFault("Catalog");
        return await response.Content.ReadFromJsonAsync<IndexPage>(ct) ?? throw new DependencyFault("Catalog");
    }
    public async Task<IndexProduct[]> PublicProducts(Guid tenant, Guid[] ids, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/public/tenants/{tenant}/catalog/published-products?" + string.Join('&', ids.Select(id => "ids=" + id)));
        using var response = await remote.Send("catalog", request, ct);
        if (!response.IsSuccessStatusCode) throw new DependencyFault("Catalog");
        return await response.Content.ReadFromJsonAsync<IndexProduct[]>(ct) ?? throw new DependencyFault("Catalog");
    }
    public async Task<bool> VerifiedPurchase(Guid tenant, Guid user, Guid product, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/tenants/{tenant}/orders/verified-purchase?user={user}&product={product}");
        using var response = await machine.Send("Order", request, ct);
        if (!response.IsSuccessStatusCode) throw new DependencyFault("Order");
        return await response.Content.ReadFromJsonAsync<bool>(ct);
    }
}
