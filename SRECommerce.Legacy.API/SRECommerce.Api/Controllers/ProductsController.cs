using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Infrastructure.Data;
using SRECommerce.Domain.Entities;
using SRECommerce.Application.DTOs;
using SRECommerce.Application.Services;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")] // -> api/products
public class ProductsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ProductsController(AppDbContext db)
    {
        _db = db;
    }

    private string NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return "";
        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return imageUrl;
        }

        return imageUrl;
    }

    // ✅ LIST (sirf ek hi GET /api/products)
    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductDto>>> List(
        [FromQuery] string? q,
        [FromQuery] int? categoryId,
        [FromQuery] string? categorySlug,   // optional slug filter
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12,
        [FromQuery] string? sort = "newest")
    {
        // 1) Agar slug aaya hai to usse categoryId nikaal lo
        int? categoryIdFromSlug = null;

        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            categoryIdFromSlug = await _db.Categories
                .Where(c => c.Slug == categorySlug)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            // slug galat hai -> empty result
            if (!categoryIdFromSlug.HasValue)
            {
                return Ok(new PagedResult<ProductDto>(
                    new List<ProductDto>(),
                    0,
                    page,
                    pageSize
                ));
            }
        }

        // 2) Base query
        var qry = _db.Products.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            qry = qry.Where(p => p.Name.Contains(q));

        if (categoryId.HasValue)
            qry = qry.Where(p => p.CategoryId == categoryId.Value);

        if (categoryIdFromSlug.HasValue)
            qry = qry.Where(p => p.CategoryId == categoryIdFromSlug.Value);

        // 3) Sorting
        qry = sort switch
        {
            "price-asc" => qry.OrderBy(p => (double)p.Price),
            "price-desc" => qry.OrderByDescending(p => (double)p.Price),
            "name" => qry.OrderBy(p => p.Name),
            _ => qry.OrderByDescending(p => p.Id) // newest
        };

        var total = await qry.CountAsync();

        var items = await qry
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductDto(
                p.Id,
                p.Name,
                p.Slug,
                p.Sku,
                p.Price,
                p.Discount,
                p.StockQty,
                p.CategoryId,
                p.ImageUrl
            ))
            .ToListAsync();

        return Ok(new PagedResult<ProductDto>(items, total, page, pageSize));
    }

    // ✅ GET /api/products/{slug}
    [HttpGet("{slug}")]
    public async Task<ActionResult<ProductDto>> BySlug(string slug)
    {
        var p = await _db.Products.FirstOrDefaultAsync(x => x.Slug == slug);
        if (p is null) return NotFound();

        return new ProductDto(
            p.Id,
            p.Name,
            p.Slug,
            p.Sku,
            p.Price,
            p.Discount,
            p.StockQty,
            p.CategoryId,
            p.ImageUrl
        );
    }

    // ✅ POST /api/products (Admin only)
    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ProductDto>> Create(CreateProductRequest req)
    {
        var name = req.Name?.Trim() ?? throw new ArgumentException("Name required");
        var slug = string.IsNullOrWhiteSpace(req.Slug)
            ? SlugHelper.ToSlug(name)
            : SlugHelper.ToSlug(req.Slug!);

        if (await _db.Products.AnyAsync(p => p.Slug == slug))
            return Conflict(new { message = "Slug already exists" });

        var entity = new Product
        {
            Name = name,
            Slug = slug,
            Sku = req.Sku,
            Price = req.Price,
            Discount = req.Discount,
            StockQty = req.StockQty,
            CategoryId = req.CategoryId,
            ImageUrl = NormalizeImageUrl(req.ImageUrl)
        };

        _db.Products.Add(entity);
        await _db.SaveChangesAsync();

        var dto = new ProductDto(
            entity.Id,
            entity.Name,
            entity.Slug,
            entity.Sku,
            entity.Price,
            entity.Discount,
            entity.StockQty,
            entity.CategoryId,
            entity.ImageUrl
        );

        return CreatedAtAction(nameof(BySlug), new { slug = entity.Slug }, dto);
    }

    // ✅ PUT /api/products/{id} (Admin only)
    [HttpPut("{id}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Update(int id, CreateProductRequest req)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null) return NotFound();

        p.Name = req.Name.Trim();
        p.Slug = string.IsNullOrWhiteSpace(req.Slug)
            ? SlugHelper.ToSlug(p.Name)
            : SlugHelper.ToSlug(req.Slug!);
        p.Sku = req.Sku;
        p.Price = req.Price;
        p.Discount = req.Discount;
        p.StockQty = req.StockQty;
        p.CategoryId = req.CategoryId;
        p.ImageUrl = NormalizeImageUrl(req.ImageUrl);

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // ✅ DELETE /api/products/{id} (Admin only)
    [HttpDelete("{id}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p is null) return NotFound();

        _db.Remove(p);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}

