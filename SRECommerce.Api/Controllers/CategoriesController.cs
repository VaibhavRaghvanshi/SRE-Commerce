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
[Route("api/[controller]")]
public class CategoriesController(AppDbContext db) : ControllerBase
{
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
    // ✅ GET: /api/categories (public)
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetAll() =>
        Ok(await db.Categories
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(
                c.Id,
                c.Name,
                c.Slug,
                c.ParentId,
                c.ImageUrl
            ))
            .ToListAsync());

    // ✅ POST: /api/categories (admin only)
    [HttpPost, Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CategoryDto>> Create(CreateCategoryRequest req)
    {
        var name = req.Name?.Trim() ?? throw new ArgumentException("Name required");
        var slug = string.IsNullOrWhiteSpace(req.Slug)
            ? SlugHelper.ToSlug(name)
            : SlugHelper.ToSlug(req.Slug!);

        if (await db.Categories.AnyAsync(c => c.Slug == slug))
            return Conflict(new { message = "Slug already exists" });

        var entity = new Category
        {
            Name = name,
            Slug = slug,
            ParentId = req.ParentId,
            ImageUrl = NormalizeImageUrl(req.ImageUrl)
        };

        db.Categories.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetBySlug),
            new { slug = entity.Slug },
            new CategoryDto(
                entity.Id,
                entity.Name,
                entity.Slug,
                entity.ParentId,
                entity.ImageUrl
            )
        );
    }

    // ✅ GET: /api/categories/{slug} (public)
    [HttpGet("{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<CategoryDto>> GetBySlug(string slug)
    {
        var c = await db.Categories.FirstOrDefaultAsync(x => x.Slug == slug);
        if (c is null) return NotFound();

        return new CategoryDto(
            c.Id,
            c.Name,
            c.Slug,
            c.ParentId,
            c.ImageUrl
        );
    }

    // ✅ PUT: /api/categories/{id} (admin only)
    [HttpPut("{id}"), Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Update(int id, CreateCategoryRequest req)
    {
        var c = await db.Categories.FindAsync(id);
        if (c is null) return NotFound();

        c.Name = req.Name.Trim();
        c.Slug = string.IsNullOrWhiteSpace(req.Slug)
            ? SlugHelper.ToSlug(c.Name)
            : SlugHelper.ToSlug(req.Slug!);
        c.ParentId = req.ParentId;
        c.ImageUrl = NormalizeImageUrl(req.ImageUrl);

        await db.SaveChangesAsync();
        return NoContent();
    }

    // ✅ DELETE: /api/categories/{id} (admin only)
    [HttpDelete("{id}"), Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await db.Categories.FindAsync(id);
        if (c is null) return NotFound();

        db.Remove(c);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

