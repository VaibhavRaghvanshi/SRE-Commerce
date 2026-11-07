using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SRECommerce.Identity.Application;

namespace SRECommerce.Identity.Infrastructure;

public sealed class HttpStoreDirectory(IHttpClientFactory clients) : IStoreDirectory
{
    private sealed record Store(Guid TenantId, string[]? Modules);
    public async Task<StoreAvailability> CheckAsync(Guid tenantId, CancellationToken ct)
    {
        try
        {
            using var response = await clients.CreateClient("store-directory")
                .GetAsync($"api/v1/public/tenants/by-id/{tenantId}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound) return StoreAvailability.NotFound;
            if (!response.IsSuccessStatusCode) return StoreAvailability.Unavailable;
            var store = await response.Content.ReadFromJsonAsync<Store>(cancellationToken: ct);
            return store?.TenantId == tenantId && store.Modules?.Contains("catalog", StringComparer.Ordinal) == true
                ? StoreAvailability.Available : StoreAvailability.NotFound;
        }
        catch (HttpRequestException) { return StoreAvailability.Unavailable; }
        catch (JsonException) { return StoreAvailability.Unavailable; }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { return StoreAvailability.Unavailable; }
    }
}
