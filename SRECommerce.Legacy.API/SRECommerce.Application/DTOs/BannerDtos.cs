namespace SRECommerce.Application.DTOs;

public record BannerDto(
    int Id,
    string Title,
    string ImageUrl,
    int LinkType,
    string? LinkValue,
    bool IsEnabled,
    int SortOrder
);

public record UpsertBannerRequest(
    string Title,
    string ImageUrl,
    int LinkType,
    string? LinkValue,
    bool IsEnabled,
    int SortOrder
);

