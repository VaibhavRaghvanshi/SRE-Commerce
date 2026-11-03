using System.Text.Json;
using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Discovery.Domain;

namespace SRECommerce.Discovery.Application;

public sealed class DiscoveryService(IDiscoveryStore store, IDiscoveryAccess access, IDiscoverySources sources, TimeProvider clock)
{
    private static readonly Regex SearchWords = new(@"[\p{L}\p{Nd}]+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex AttributeKey = new(@"\A[a-zA-Z0-9_-]{1,64}\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static bool Public(IndexProduct p) => p.Visible && !p.B2BOnly && p.PublishedRevision > 0;
    private static ReviewView View(ProductReview row) => new(row.Id, row.ProductId, row.Rating, row.Body, row.State, Convert.ToBase64String(row.Version), row.UpdatedAt);
    private static SearchResult Hit(IndexProduct row) => new(row.Id, row.Name, row.Slug, row.CategoryId, row.BrandId, row.PublishedRevision);
    private static string Bounded(string value, int size) => value.Length <= size ? value : value[..size];
    private static void Page(int page) { if (page is < 1 or > 1000) throw new BusinessFault("InvalidDiscoveryPage", 400); }
    private static string Text(string value, int size)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > size || value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) || value.Contains('<') || value.Contains('>'))
            throw new BusinessFault("InvalidReviewText", 400);
        return value.Trim();
    }
    private static Dictionary<string, string> Attributes(IndexProduct row) => row.Attributes
        .Where(pair => AttributeKey.IsMatch(pair.Key) && pair.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
        .Take(30).ToDictionary(pair => pair.Key, pair => Bounded(pair.Value.ToString(), 120), StringComparer.Ordinal);

    public async Task<IndexPage> Reindex(Guid tenant, int page, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); Page(page);
        var source = await sources.CatalogPage(tenant, page, ct);
        if (source.Page != page || source.Items.Length > 50 || source.Items.Select(x => x.Id).Distinct().Count() != source.Items.Length)
            throw new DependencyFault("Catalog");
        var products = source.Items.Where(Public).ToArray();
        await store.Transaction(async token =>
        {
            var existing = (await store.Documents(products.Select(x => x.Id).ToArray(), token)).ToDictionary(x => x.ProductId);
            foreach (var p in products)
            {
                if (!existing.TryGetValue(p.Id, out var row)) { row = new() { TenantId = tenant, ProductId = p.Id }; store.Add(row); }
                if (row.Revision >= p.PublishedRevision) continue;
                var attributes = Attributes(p);
                row.Revision = p.PublishedRevision; row.Name = Bounded(p.Name, 200); row.CategoryId = p.CategoryId; row.BrandId = p.BrandId;
                row.SearchText = Bounded(string.Join(' ', new[] { p.Name, p.Description }.Concat(p.Variants.Where(v => v.Active).Select(v => v.Sku)).Concat(attributes.Values)), 6000);
                row.Attributes = ContractJson.Write(attributes); row.IndexedAt = clock.GetUtcNow();
            }
            await store.Save("discovery.index.page", page.ToString(System.Globalization.CultureInfo.InvariantCulture), token); return true;
        }, ct);
        return source with { Items = products };
    }

    private static string? Condition(string? q)
    {
        if (string.IsNullOrWhiteSpace(q)) return null;
        if (q.Length > 120) throw new BusinessFault("SearchTextTooLong", 400);
        var words = SearchWords.Matches(q).Select(x => x.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (words.Length is < 1 or > 8 || words.Any(x => x.Length > 36)) throw new BusinessFault("InvalidSearchTerms", 400);
        return string.Join(" AND ", words.Select(x => $"\"{x}*\""));
    }

    public async Task<SearchPage> Search(Guid tenant, SearchQuery query, CancellationToken ct)
    {
        await access.Public(tenant, ct); Page(query.Page);
        if (query.Sort is not ("relevance" or "name" or "recent") || (query.Attribute is null) != (query.Value is null) ||
            query.Attribute is not null && (!AttributeKey.IsMatch(query.Attribute) || query.Value!.Length is < 1 or > 120))
            throw new BusinessFault("InvalidSearchFilters", 400);
        var documents = await store.Search(Condition(query.Q), query, ct);
        var candidates = documents.Take(24).ToArray();
        var catalog = candidates.Length == 0 ? [] : await sources.PublicProducts(tenant, candidates.Select(x => x.ProductId).ToArray(), ct);
        var live = catalog.Where(Public).ToDictionary(x => x.Id);
        var visible = candidates.Where(x => live.TryGetValue(x.ProductId, out var p) && p.PublishedRevision == x.Revision).Select(x => live[x.ProductId]).ToArray();
        var categories = visible.GroupBy(x => x.CategoryId).ToDictionary(x => x.Key, x => x.Count());
        var attributes = visible.SelectMany(Attributes).GroupBy(x => x.Key).ToDictionary(x => x.Key, x => x.Select(a => a.Value).Distinct().Order().Take(20).ToArray());
        return new(visible.Select(Hit).ToArray(), query.Page, documents.Length > 24, categories, attributes);
    }

    public async Task<SearchResult[]> Related(Guid tenant, Guid product, CancellationToken ct)
    {
        await access.Public(tenant, ct);
        var source = await Current(tenant, product, ct);
        return (await Search(tenant, new(Category: source.CategoryId, Sort: "name"), ct)).Items.Where(x => x.ProductId != product).Take(6).ToArray();
    }

    private async Task<IndexProduct> Current(Guid tenant, Guid product, CancellationToken ct) =>
        (await sources.PublicProducts(tenant, [product], ct)).SingleOrDefault(p => p.Id == product && Public(p)) ?? throw new BusinessFault("ProductNotFound", 404);

    public async Task<PublicReview[]> PublicReviews(Guid tenant, Guid product, int page, CancellationToken ct)
    {
        await access.Public(tenant, ct); Page(page); await Current(tenant, product, ct);
        return (await store.Reviews(product, ReviewState.Approved, page, ct)).Select(x => new PublicReview(x.Rating, x.Body, true, x.UpdatedAt)).ToArray();
    }

    public async Task<ReviewView?> MyReview(Guid tenant, Guid product, CancellationToken ct)
    {
        var user = await access.Human(tenant, ActorPermission.Customer, ct);
        return await store.MyReview(product, user, ct) is {} row ? View(row) : null;
    }

    public async Task<ReviewView> SaveReview(Guid tenant, Guid product, ReviewInput input, CancellationToken ct)
    {
        var user = await access.Human(tenant, ActorPermission.Customer, ct);
        if (input.Rating is < 1 or > 5) throw new BusinessFault("InvalidReviewRating", 400);
        var body = Text(input.Body, 1500);
        await Current(tenant, product, ct);
        if (!await sources.VerifiedPurchase(tenant, user, product, ct)) throw new BusinessFault("VerifiedPurchaseRequired", 403);
        return await store.Transaction(async token =>
        {
            var row = await store.MyReview(product, user, token);
            if (row is null)
            {
                if (input.Version is not null) throw new BusinessFault("VersionConflict");
                row = new() { Id = Guid.NewGuid(), TenantId = tenant, ProductId = product, UserId = user }; store.Add(row);
            }
            else store.Version(row, input.Version ?? throw new BusinessFault("VersionRequired", 400));
            row.Rating = input.Rating; row.Body = body; row.State = ReviewState.Pending; row.UpdatedAt = clock.GetUtcNow();
            await store.Save("discovery.review.submitted", row.Id.ToString(), token); return View(row);
        }, ct);
    }

    public async Task<ReviewView[]> Moderation(Guid tenant, ReviewState state, int page, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); Page(page);
        if (!Enum.IsDefined(state)) throw new BusinessFault("InvalidReviewState", 400);
        return (await store.Reviews(null, state, page, ct)).Select(View).ToArray();
    }

    public async Task<ReviewView> Moderate(Guid tenant, Guid review, ModerationInput input, CancellationToken ct)
    {
        var moderator = await access.Human(tenant, ActorPermission.Manager, ct);
        if (input.State is not (ReviewState.Approved or ReviewState.Rejected)) throw new BusinessFault("InvalidModerationDecision", 400);
        var reason = Text(input.Reason, 300);
        return await store.Transaction(async token =>
        {
            var row = await store.Review(review, token) ?? throw new BusinessFault("ReviewNotFound", 404);
            if (row.UserId == moderator) throw new BusinessFault("CannotModerateOwnReview", 403);
            store.Version(row, input.Version);
            if (row.State == input.State) return View(row);
            row.State = input.State;
            store.Add(new ReviewDecision { TenantId = tenant, Id = Guid.NewGuid(), ReviewId = review, ModeratorId = moderator, State = input.State, Reason = reason, CreatedAt = clock.GetUtcNow() });
            await store.Save("discovery.review.moderated", review.ToString(), token); return View(row);
        }, ct);
    }
}
