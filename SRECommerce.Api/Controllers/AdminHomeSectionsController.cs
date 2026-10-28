using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Infrastructure.Data;
using SRECommerce.Domain.Entities;
using SRECommerce.Application.DTOs;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/admin/home-sections")]
[Authorize(Roles = Roles.Staff)]
public class AdminHomeSectionsController(AppDbContext db) : ControllerBase
{
    private static int ExpectedEntityType(int sectionType)
    {
        return sectionType switch
        {
            1 => (int)HomeEntityType.Category,
            2 => (int)HomeEntityType.Banner,
            3 => (int)HomeEntityType.Product,
            _ => 0
        };
    }

    [HttpGet]
    public async Task<ActionResult<List<HomeSectionDto>>> List()
    {
        var items = await db.HomeSections
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new HomeSectionDto(
                x.Id, x.Type, x.Title, x.IsEnabled, x.SortOrder,
                x.ItemsToShow, x.CardsPerRow, x.SourceKey, x.StyleJson
            ))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<HomeSectionDto>> Create(UpsertHomeSectionRequest req)
    {
        var entity = new HomeSection
        {
            Type = req.Type,
            Title = req.Title,
            IsEnabled = req.IsEnabled,
            SortOrder = req.SortOrder,
            ItemsToShow = req.ItemsToShow,
            CardsPerRow = req.CardsPerRow,
            SourceKey = req.SourceKey,
            StyleJson = req.StyleJson
        };

        db.HomeSections.Add(entity);
        await db.SaveChangesAsync();

        var dto = new HomeSectionDto(
            entity.Id, entity.Type, entity.Title, entity.IsEnabled, entity.SortOrder,
            entity.ItemsToShow, entity.CardsPerRow, entity.SourceKey, entity.StyleJson
        );

        return CreatedAtAction(nameof(Get), new { id = entity.Id }, dto);
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<HomeSectionDto>> Get(int id)
    {
        var x = await db.HomeSections.FindAsync(id);
        if (x is null) return NotFound();

        return Ok(new HomeSectionDto(
            x.Id, x.Type, x.Title, x.IsEnabled, x.SortOrder,
            x.ItemsToShow, x.CardsPerRow, x.SourceKey, x.StyleJson
        ));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertHomeSectionRequest req)
    {
        var x = await db.HomeSections.FindAsync(id);
        if (x is null) return NotFound();

        x.Type = req.Type;
        x.Title = req.Title;
        x.IsEnabled = req.IsEnabled;
        x.SortOrder = req.SortOrder;
        x.ItemsToShow = req.ItemsToShow;
        x.CardsPerRow = req.CardsPerRow;
        x.SourceKey = req.SourceKey;
        x.StyleJson = req.StyleJson;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var x = await db.HomeSections.FindAsync(id);
        if (x is null) return NotFound();

        db.HomeSections.Remove(x);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/items")]
    public async Task<ActionResult<List<HomeSectionItemDto>>> ListItems(int id)
    {
        var section = await db.HomeSections.FindAsync(id);
        if (section is null) return NotFound();

        var items = await db.HomeSectionItems
            .Where(i => i.HomeSectionId == id)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .ToListAsync();

        var categoryIds = items.Where(i => i.EntityType == HomeEntityType.Category).Select(i => i.EntityId).Distinct().ToList();
        var productIds = items.Where(i => i.EntityType == HomeEntityType.Product).Select(i => i.EntityId).Distinct().ToList();
        var bannerIds = items.Where(i => i.EntityType == HomeEntityType.Banner).Select(i => i.EntityId).Distinct().ToList();

        var categories = await db.Categories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.Name, c.ImageUrl });

        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => new { p.Name, p.ImageUrl });

        var banners = await db.Banners
            .Where(b => bannerIds.Contains(b.Id))
            .ToDictionaryAsync(b => b.Id, b => new { b.Title, b.ImageUrl });

        var dto = items.Select(i =>
        {
            string? title = null;
            string? image = null;

            if (i.EntityType == HomeEntityType.Category && categories.TryGetValue(i.EntityId, out var c))
            {
                title = c.Name;
                image = c.ImageUrl;
            }
            else if (i.EntityType == HomeEntityType.Product && products.TryGetValue(i.EntityId, out var p))
            {
                title = p.Name;
                image = p.ImageUrl;
            }
            else if (i.EntityType == HomeEntityType.Banner && banners.TryGetValue(i.EntityId, out var b))
            {
                title = b.Title;
                image = b.ImageUrl;
            }

            return new HomeSectionItemDto(
                i.Id,
                (int)i.EntityType,
                i.EntityId,
                i.SortOrder,
                i.IsEnabled,
                title,
                image
            );
        }).ToList();

        return Ok(dto);
    }

    [HttpPut("{id:int}/items")]
    public async Task<IActionResult> SaveItems(int id, UpsertHomeSectionItemsRequest req)
    {
        var section = await db.HomeSections.FindAsync(id);
        if (section is null) return NotFound();

        var expected = ExpectedEntityType(section.Type);
        if (expected == 0) return BadRequest(new { message = "Invalid section type" });

        var items = (req.Items ?? new List<HomeSectionItemUpsert>())
            .Where(x => x.EntityType == expected)
            .ToList();

        var existing = await db.HomeSectionItems.Where(i => i.HomeSectionId == id).ToListAsync();
        db.HomeSectionItems.RemoveRange(existing);

        foreach (var item in items)
        {
            db.HomeSectionItems.Add(new HomeSectionItem
            {
                HomeSectionId = id,
                EntityType = (HomeEntityType)item.EntityType,
                EntityId = item.EntityId,
                SortOrder = item.SortOrder,
                IsEnabled = item.IsEnabled
            });
        }

        await db.SaveChangesAsync();
        return NoContent();
    }
}

