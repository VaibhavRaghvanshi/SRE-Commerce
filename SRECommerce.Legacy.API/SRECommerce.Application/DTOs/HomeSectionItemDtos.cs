namespace SRECommerce.Application.DTOs;

public record HomeSectionItemDto(
    int Id,
    int EntityType,
    int EntityId,
    int SortOrder,
    bool IsEnabled,
    string? Title,
    string? ImageUrl
);

public record HomeSectionItemUpsert(
    int EntityType,
    int EntityId,
    int SortOrder,
    bool IsEnabled
);

public record UpsertHomeSectionItemsRequest(List<HomeSectionItemUpsert> Items);

