namespace SRECommerce.Application.DTOs;

public record CategoryDto(int Id, string Name, string Slug, int? ParentId, string? ImageUrl);
public record CreateCategoryRequest
{
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    public int? ParentId { get; set; }
    public string? ImageUrl { get; set; }
}


public record ProductDto(int Id, string Name, string Slug, string Sku, decimal Price, decimal? Discount,
                         int StockQty, int CategoryId, string? ImageUrl);
public record CreateProductRequest(string Name, string? Slug, string Sku, decimal Price, decimal? Discount,
                                   int StockQty, int CategoryId, string? ImageUrl);

public record PagedResult<T>(List<T> Items, int TotalItems, int Page, int PageSize);

public record HomeSectionDto(
    int Id, int Type, string? Title, bool IsEnabled, int SortOrder,
    int ItemsToShow, int CardsPerRow, string? SourceKey, string? StyleJson
);

public record UpsertHomeSectionRequest(
    int Type, string? Title, bool IsEnabled, int SortOrder,
    int ItemsToShow, int CardsPerRow, string? SourceKey, string? StyleJson
);



