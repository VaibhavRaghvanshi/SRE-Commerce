namespace SRECommerce.Domain.Entities;
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Sku { get; set; } = "";
    public decimal Price { get; set; }
    public decimal? Discount { get; set; }
    public int StockQty { get; set; }
    public int CategoryId { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

