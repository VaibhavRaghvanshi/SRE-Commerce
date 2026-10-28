using SRECommerce.Domain.Entities;

namespace SRECommerce.Application.DTOs;
public record HomeResponse(List<HomeBlock> Sections);

public record HomeBlock(
    int Type,
    string? Title,
    int SortOrder,
    int ItemsToShow,
    int CardsPerRow,
    string? SourceKey,
    string? StyleJson,
    object Items
);

