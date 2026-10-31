namespace SRECommerce.Domain.Entities;

public enum HomeEntityType
{
    Category = 1,
    Product = 2,
    Banner = 3
}

public class HomeSectionItem
{
    public int Id { get; set; }

    public int HomeSectionId { get; set; }
    public HomeSection HomeSection { get; set; } = null!;

    public HomeEntityType EntityType { get; set; }
    public int EntityId { get; set; } // CategoryId/ProductId/BannerId

    public int SortOrder { get; set; } = 1;
    public bool IsEnabled { get; set; } = true;
}

