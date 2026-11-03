using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Discovery.Application;
using SRECommerce.Discovery.Domain;
using SRECommerce.Discovery.Infrastructure;
using Xunit;

namespace SRECommerce.Discovery.Tests;

public sealed class DiscoverySqlTests
{
    private sealed class Access(ModuleScope scope) : IDiscoveryAccess
    {
        public Guid User { get; set; } = Guid.NewGuid();
        public Task Public(Guid tenant, CancellationToken ct) { scope.BindPublic(tenant); return Task.CompletedTask; }
        public Task<Guid> Human(Guid tenant, ActorPermission role, CancellationToken ct)
        { scope.BindUser(tenant, new(User, "Customer", false, tenant, "Customer", [], [])); return Task.FromResult(User); }
    }
    private sealed class Catalog(Guid tenant, IndexProduct product) : IDiscoverySources
    {
        public IndexProduct Product { get; set; } = product;
        public bool Verified { get; set; }
        public Task<IndexPage> CatalogPage(Guid id, int page, CancellationToken ct) => Task.FromResult(new IndexPage(id == tenant ? [Product] : [], 1, page, 50));
        public Task<IndexProduct[]> PublicProducts(Guid id, Guid[] ids, CancellationToken ct) => Task.FromResult<IndexProduct[]>(id == tenant && ids.Contains(Product.Id) ? [Product] : []);
        public Task<bool> VerifiedPurchase(Guid id, Guid user, Guid item, CancellationToken ct) => Task.FromResult(Verified && id == tenant && item == Product.Id);
    }

    [Fact]
    public async Task FullTextIsTenantScopedAndReviewsRequirePurchaseAndModeration()
    {
        var name = "SRECommerce_DiscoveryTests_" + Guid.NewGuid().ToString("N");
        var tenant = Guid.NewGuid();
        var product = new IndexProduct(Guid.NewGuid(), "Masala saffron blend", "masala-saffron", Guid.NewGuid(), null, "Freshly ground blend",
            true, false, new() { ["size"] = JsonSerializer.SerializeToElement("50g") }, [new("SPICE-01", true)], 1);
        var scope = new ModuleScope();
        var access = new Access(scope); var sources = new Catalog(tenant, product);
        await using var db = new DiscoveryDbContext(new DbContextOptionsBuilder<DiscoveryDbContext>()
            .UseSqlServer(ModuleDatabase.Check(ServiceManifest.Get("Discovery"), $"Server=localhost,1433;Database={name};Integrated Security=true;Encrypt=true;TrustServerCertificate=true", true)).Options, scope);
        await db.Database.MigrateAsync();
        var service = new DiscoveryService(new DiscoveryStore(db, scope, new HttpContextAccessor()), access, sources, TimeProvider.System);
        Assert.Single((await service.Reindex(tenant, 1, default)).Items);
        var filters = new SearchQuery("saffr", product.CategoryId, Attribute: "size", Value: "50g");
        SearchPage result = await service.Search(tenant, filters, default);
        for (var i = 0; result.Items.Length == 0 && i < 80; i++)
        { await Task.Delay(250); result = await service.Search(tenant, filters, default); }
        Assert.Equal(product.Id, Assert.Single(result.Items).ProductId);
        Assert.Contains("50g", result.PageAttributes["size"]);
        Assert.Empty((await service.Search(Guid.NewGuid(), filters, default)).Items);
        sources.Product = product with { B2BOnly = true };
        Assert.Empty((await service.Search(tenant, filters, default)).Items);
        sources.Product = product;
        var input = new ReviewInput(5, "A useful test review", null);
        Assert.Equal("VerifiedPurchaseRequired", (await Assert.ThrowsAsync<BusinessFault>(() => service.SaveReview(tenant, product.Id, input, default))).Code);
        sources.Verified = true;
        var review = await service.SaveReview(tenant, product.Id, input, default);
        Assert.Empty(await service.PublicReviews(tenant, product.Id, 1, default));
        Assert.Equal("CannotModerateOwnReview", (await Assert.ThrowsAsync<BusinessFault>(() => service.Moderate(tenant, review.Id, new(ReviewState.Approved, "Checked", review.Version), default))).Code);
        access.User = Guid.NewGuid();
        _ = await service.Moderate(tenant, review.Id, new(ReviewState.Approved, "Checked", review.Version), default);
        Assert.True(Assert.Single(await service.PublicReviews(tenant, product.Id, 1, default)).VerifiedPurchase);
        Assert.Empty(await service.Moderation(Guid.NewGuid(), ReviewState.Approved, 1, default));
    }
}
