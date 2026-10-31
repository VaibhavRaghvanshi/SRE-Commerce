namespace SRECommerce.Domain.Entities;
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int? ParentId { get; set; }
    public string? ImageUrl { get; set; }

}

