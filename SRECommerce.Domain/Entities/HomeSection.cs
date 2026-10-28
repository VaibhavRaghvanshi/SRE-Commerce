namespace SRECommerce.Domain.Entities;

public class HomeSection
{
    public int Id { get; set; }

    public int Type { get; set; }               // 1=Categories, 2=Banners, 3=Products (simple int)
    public string? Title { get; set; }

    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; } = 1;

    public int ItemsToShow { get; set; } = 8;
    public int CardsPerRow { get; set; } = 4;

    public string? SourceKey { get; set; }      // optional grouping key
    public string? StyleJson { get; set; }      // optional style config

    // ✅ This is the missing property that your error is shouting about
    public List<HomeSectionItem> Items { get; set; } = new();
}

