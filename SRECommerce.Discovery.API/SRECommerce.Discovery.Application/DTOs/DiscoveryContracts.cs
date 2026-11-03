using System.Text.Json;
using SRECommerce.Discovery.Domain;

namespace SRECommerce.Discovery.Application;

public sealed record IndexVariant(string Sku, bool Active);
public sealed record IndexProduct(Guid Id, string Name, string Slug, Guid CategoryId, Guid? BrandId, string Description,
    bool Visible, bool B2BOnly, Dictionary<string, JsonElement> Attributes, IndexVariant[] Variants, int PublishedRevision);
public sealed record IndexPage(IndexProduct[] Items, int Total, int Page, int PageSize);
public sealed record SearchQuery(string? Q = null, Guid? Category = null, Guid? Brand = null, string? Attribute = null,
    string? Value = null, string Sort = "relevance", int Page = 1);
public sealed record SearchResult(Guid ProductId, string Name, string Slug, Guid CategoryId, Guid? BrandId, int Revision);
public sealed record SearchPage(SearchResult[] Items, int Page, bool More, IReadOnlyDictionary<Guid, int> PageCategories,
    IReadOnlyDictionary<string, string[]> PageAttributes);
public sealed record ReviewInput(int Rating, string Body, string? Version);
public sealed record ModerationInput(ReviewState State, string Reason, string Version);
public sealed record PublicReview(int Rating, string Body, bool VerifiedPurchase, DateTimeOffset UpdatedAt);
public sealed record ReviewView(Guid Id, Guid ProductId, int Rating, string Body, ReviewState State, string Version, DateTimeOffset UpdatedAt);
