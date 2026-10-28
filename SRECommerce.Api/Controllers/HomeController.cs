using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Infrastructure.Data;
using SRECommerce.Domain.Entities;
using SRECommerce.Application.DTOs;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/home")]
public class HomeController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HomeResponse>> Get()
    {
        var sections = await db.HomeSections
            .Where(s => s.IsEnabled)
            .OrderBy(s => s.SortOrder)
            .ToListAsync();

        var blocks = new List<HomeBlock>();

        foreach (var s in sections)
        {
            object items = Array.Empty<object>();
            var sectionItems = await db.HomeSectionItems
                .Where(i => i.HomeSectionId == s.Id && i.IsEnabled)
                .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
                .ToListAsync();

            if (s.Type == (int)HomeSectionType.CategoriesGrid)
            {
                if (sectionItems.Any())
                {
                    var ids = sectionItems.Where(i => i.EntityType == HomeEntityType.Category).Select(i => i.EntityId).ToList();
                    var map = await db.Categories
                        .Where(c => ids.Contains(c.Id))
                        .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.ImageUrl))
                        .ToListAsync();
                    var dict = map.ToDictionary(x => x.Id, x => x);
                    items = ids.Where(dict.ContainsKey).Select(id => dict[id]).Take(s.ItemsToShow).ToList();
                }
                else
                {
                    items = await db.Categories
                        .OrderBy(c => c.Name)
                        .Take(s.ItemsToShow)
                        .Select(c => new CategoryDto(c.Id, c.Name, c.Slug, c.ParentId, c.ImageUrl))
                        .ToListAsync();
                }
            }
            else if (s.Type == (int)HomeSectionType.ProductsGrid)
            {
                if (sectionItems.Any())
                {
                    var ids = sectionItems.Where(i => i.EntityType == HomeEntityType.Product).Select(i => i.EntityId).ToList();
                    var map = await db.Products
                        .Where(p => ids.Contains(p.Id))
                        .Select(p => new ProductDto(p.Id, p.Name, p.Slug, p.Sku, p.Price, p.Discount, p.StockQty, p.CategoryId, p.ImageUrl))
                        .ToListAsync();
                    var dict = map.ToDictionary(x => x.Id, x => x);
                    items = ids.Where(dict.ContainsKey).Select(id => dict[id]).Take(s.ItemsToShow).ToList();
                }
                else
                {
                    var productQuery = db.Products.AsQueryable();
                    var sourceKey = (s.SourceKey ?? "").Trim().ToLowerInvariant();

                    if (!string.IsNullOrWhiteSpace(sourceKey))
                    {
                        if (sourceKey is "deals" or "sale" or "discounts")
                        {
                            productQuery = productQuery
                                .Where(p => (p.Discount ?? 0) > 0)
                                .OrderByDescending(p => (double)(p.Discount ?? 0));
                        }
                        else if (sourceKey is "low-stock")
                        {
                            productQuery = productQuery.OrderBy(p => p.StockQty);
                        }
                        else if (sourceKey is "top-stock")
                        {
                            productQuery = productQuery.OrderByDescending(p => p.StockQty);
                        }
                        else
                        {
                            var categoryId = await db.Categories
                                .Where(c => c.Slug == sourceKey)
                                .Select(c => (int?)c.Id)
                                .FirstOrDefaultAsync();

                            if (categoryId.HasValue)
                            {
                                productQuery = productQuery.Where(p => p.CategoryId == categoryId.Value);
                            }

                            productQuery = productQuery.OrderByDescending(p => p.Id);
                        }
                    }
                    else
                    {
                        productQuery = productQuery.OrderByDescending(p => p.Id);
                    }

                    items = await productQuery
                        .Take(s.ItemsToShow)
                        .Select(p => new ProductDto(p.Id, p.Name, p.Slug, p.Sku, p.Price, p.Discount, p.StockQty, p.CategoryId, p.ImageUrl))
                        .ToListAsync();
                }
            }
            else if (s.Type == (int)HomeSectionType.BannerSlider)
            {
                if (sectionItems.Any())
                {
                    var ids = sectionItems.Where(i => i.EntityType == HomeEntityType.Banner).Select(i => i.EntityId).ToList();
                    items = await db.Banners
                        .Where(b => ids.Contains(b.Id) && b.IsEnabled)
                        .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
                        .Take(Math.Max(1, s.ItemsToShow))
                        .Select(b => new
                        {
                            imageUrl = b.ImageUrl,
                            link = b.LinkType == BannerLinkType.External ? (b.LinkValue ?? "")
                                 : b.LinkType == BannerLinkType.Product ? "/p/" + (b.LinkValue ?? "")
                                 : b.LinkType == BannerLinkType.Category ? "/c/" + (b.LinkValue ?? "")
                                 : ""
                        })
                        .ToListAsync();
                }
                else
                {
                    items = await db.Banners
                        .Where(b => b.IsEnabled)
                        .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
                        .Take(Math.Max(1, s.ItemsToShow))
                        .Select(b => new
                        {
                            imageUrl = b.ImageUrl,
                            link = b.LinkType == BannerLinkType.External ? (b.LinkValue ?? "")
                                 : b.LinkType == BannerLinkType.Product ? "/p/" + (b.LinkValue ?? "")
                                 : b.LinkType == BannerLinkType.Category ? "/c/" + (b.LinkValue ?? "")
                                 : ""
                        })
                        .ToListAsync();
                }
            }

            blocks.Add(new HomeBlock((int)s.Type, s.Title, s.SortOrder, s.ItemsToShow, s.CardsPerRow, s.SourceKey, s.StyleJson, items));
        }

        return Ok(new HomeResponse(blocks));
    }
}

