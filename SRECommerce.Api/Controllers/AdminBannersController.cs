using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Infrastructure.Data;
using SRECommerce.Domain.Entities;
using SRECommerce.Application.DTOs;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/admin/banners")]
[Authorize(Roles = Roles.Staff)]
public class AdminBannersController(AppDbContext db) : ControllerBase
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

    [HttpGet]
    public async Task<ActionResult<List<BannerDto>>> List()
    {
        var items = await db.Banners
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Id)
            .Select(x => new BannerDto(
                x.Id, x.Title, x.ImageUrl, (int)x.LinkType, x.LinkValue, x.IsEnabled, x.SortOrder
            ))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<BannerDto>> Create(UpsertBannerRequest req)
    {
        var entity = new Banner
        {
            Title = req.Title,
            ImageUrl = NormalizeImageUrl(req.ImageUrl),
            LinkType = (BannerLinkType)req.LinkType,
            LinkValue = req.LinkValue,
            IsEnabled = req.IsEnabled,
            SortOrder = req.SortOrder
        };

        db.Banners.Add(entity);
        await db.SaveChangesAsync();

        return Ok(new BannerDto(
            entity.Id, entity.Title, entity.ImageUrl, (int)entity.LinkType, entity.LinkValue, entity.IsEnabled, entity.SortOrder
        ));
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpsertBannerRequest req)
    {
        var entity = await db.Banners.FindAsync(id);
        if (entity is null) return NotFound();

        entity.Title = req.Title;
        entity.ImageUrl = NormalizeImageUrl(req.ImageUrl);
        entity.LinkType = (BannerLinkType)req.LinkType;
        entity.LinkValue = req.LinkValue;
        entity.IsEnabled = req.IsEnabled;
        entity.SortOrder = req.SortOrder;

        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await db.Banners.FindAsync(id);
        if (entity is null) return NotFound();

        db.Banners.Remove(entity);
        await db.SaveChangesAsync();
        return NoContent();
    }
}

