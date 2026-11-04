using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Discovery.Application;
using SRECommerce.Discovery.Domain;

namespace SRECommerce.Discovery.Infrastructure;

public sealed class CatalogProjectionHandler(DiscoveryDbContext db, DiscoveryService discovery) : IIntegrationHandler
{
    public IReadOnlyCollection<string> EventTypes => MessagingTopology.CatalogEvents;
    public Task HandleAsync(EventEnvelope message, CancellationToken ct)
    {
        if (message.Producer != "Catalog" || !EventTypes.Contains(message.Type))
            throw new BusinessFault("InvalidCatalogProducer", 400);
        return IntegrationInbox.Apply(db, message, async token =>
        {
            if (message.Type == "catalog.product.published.v1")
            {
                var product = message.Data.Deserialize<IndexProduct>(new JsonSerializerOptions(JsonSerializerDefaults.Web))
                    ?? throw new BusinessFault("InvalidCatalogProjection", 400);
                await discovery.ProjectPublication(message.TenantId, product, token);
                return;
            }
            if (!message.Data.TryGetProperty("productId", out var value) || !value.TryGetGuid(out var id) || id == Guid.Empty)
                throw new BusinessFault("InvalidArchivedProduct", 400);
            var row = await db.Documents.SingleOrDefaultAsync(document => document.ProductId == id, token);
            if (row is null)
            {
                row = new SearchDocument { TenantId = message.TenantId, ProductId = id };
                db.Documents.Add(row);
            }
            row.Revision = int.MaxValue;
            row.Name = ""; row.SearchText = ""; row.Attributes = "{}";
            row.IndexedAt = DateTimeOffset.UtcNow;
        }, ct);
    }
}
