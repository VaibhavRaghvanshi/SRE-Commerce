using System.Net;
using System.Net.Http.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Manufacturing.Application;
using SRECommerce.Manufacturing.Domain;

namespace SRECommerce.Manufacturing.Infrastructure;

public sealed class ManufacturingAccess(ModuleAccess access, ModuleScope scope, RemoteAccess remote) : IManufacturingAccess
{
    public Guid? Actor => scope.ActorId;
    public Task Human(Guid tenant, ActorPermission role, CancellationToken ct) => access.AuthorizeAsync(tenant, role, ct);
    public async Task<string> Currency(Guid tenant, CancellationToken ct)
    {
        var store = await remote.PublicTenantAsync(tenant, ct);
        if (store is null || store.TenantId != tenant) throw new BusinessFault("ManufacturingTenantNotPublished", 403);
        return store.Currency;
    }
}

public sealed class ManufacturingInventory(ServiceHttp client, TimeProvider clock) : IManufacturingInventory
{
    private sealed record StockOption(Guid WarehouseId, StockItem[] Items);
    private sealed record StockItem(Guid VariantId, bool Tracked);
    private static string Base(Guid tenant) => $"internal/v1/tenants/{tenant}/inventory/";
    private async Task<HttpResponseMessage> Send(Guid tenant, HttpMethod method, string suffix, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, Base(tenant) + suffix);
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await client.Send("Inventory", request, ct);
        if (response.IsSuccessStatusCode) return response;
        using (response)
        {
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Conflict or HttpStatusCode.Forbidden)
            {
                var problem = await response.Content.ReadFromJsonAsync<RemoteProblem>(ct);
                var code = problem?.Title;
                throw new BusinessFault(code is { Length: > 0 and <= 80 } && code.All(c => char.IsAsciiLetterOrDigit(c)) ? code : "InventoryRejectedProduction", (int)response.StatusCode);
            }
            throw new DependencyFault("Inventory");
        }
    }
    private sealed record RemoteProblem(string? Title);
    public async Task Validate(Guid tenant, Guid warehouse, Guid[] variants, CancellationToken ct)
    {
        var deadline = Uri.EscapeDataString(clock.GetUtcNow().AddMinutes(5).ToString("O"));
        using var response = await Send(tenant, HttpMethod.Get, "allocation-options?expiresAt=" + deadline + "&" + string.Join('&', variants.Select(v => "variants=" + v)), null, ct);
        var options = await response.Content.ReadFromJsonAsync<StockOption[]>(ct) ?? throw new DependencyFault("Inventory");
        var tracked = options.SingleOrDefault(x => x.WarehouseId == warehouse)?.Items.Where(x => x.Tracked).Select(x => x.VariantId).ToHashSet();
        if (tracked is null || variants.Any(v => !tracked.Contains(v))) throw new BusinessFault("ProductionWarehouseOrStockItemUnavailable");
    }
    private static async Task<InventoryReservation> Read(HttpResponseMessage response, Guid expected, CancellationToken ct)
    {
        var value = await response.Content.ReadFromJsonAsync<InventoryReservation>(ct);
        return value is not null && value.Id == expected ? value : throw new DependencyFault("Inventory");
    }
    public async Task<InventoryReservation> Reserve(ProductionOrder order, ProductionMaterial[] materials, CancellationToken ct)
    {
        using var response = await Send(order.TenantId, HttpMethod.Post, "reservations", new { order.Id, order.WarehouseId,
            Reference = "Manufacturing " + order.Id.ToString("N"), ExpiresAt = order.ReservationExpiresAt, Items = materials }, ct);
        return await Read(response, order.Id, ct);
    }
    public async Task<InventoryReservation> Consume(ProductionOrder order, CancellationToken ct)
    {
        using var current = await Send(order.TenantId, HttpMethod.Get, $"reservations/{order.Id}", null, ct);
        var reservation = await Read(current, order.Id, ct);
        if (reservation.Status != 0) return reservation;
        using var response = await Send(order.TenantId, HttpMethod.Post, $"reservations/{order.Id}/commit", new { reservation.Version }, ct);
        return await Read(response, order.Id, ct);
    }
    public async Task Cancel(ProductionOrder order, CancellationToken ct)
    {
        using var response = await Send(order.TenantId, HttpMethod.Post, $"reservations/{order.Id}/cancel",
            new { OperationId = order.Id, Reason = "Production cancelled before processing" }, ct);
    }
    public async Task Receive(ProductionOrder order, CancellationToken ct)
    {
        using var response = await Send(order.TenantId, HttpMethod.Post, "manufacturing-receipts", new { OperationId = order.Id, ProductionId = order.Id,
            order.WarehouseId, VariantId = order.OutputVariantId, Quantity = order.GoodQuantity, ExpiresAt = order.OutputExpiresAt }, ct);
    }
}
