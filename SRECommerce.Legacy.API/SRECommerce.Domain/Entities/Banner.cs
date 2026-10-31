namespace SRECommerce.Domain.Entities;

public enum BannerLinkType
{
    None = 0,
    Product = 1,
    Category = 2,
    External = 3
}

public class Banner
{
    public int Id { get; set; }
    public string Title { get; set; } = "";
    public string ImageUrl { get; set; } = "";

    public BannerLinkType LinkType { get; set; } = BannerLinkType.None;
    public string? LinkValue { get; set; } // productSlug/categorySlug/url

    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; } = 1;
}

