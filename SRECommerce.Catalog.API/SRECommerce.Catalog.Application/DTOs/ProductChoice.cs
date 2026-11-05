using SRECommerce.Catalog.Domain;

namespace SRECommerce.Catalog.Application;

public sealed record ProductChoice(Guid Id, string Name, string Slug, ProductStatus Status, int PublishedRevision);
public sealed record ProductChoicesRequest(Guid[] Ids);
