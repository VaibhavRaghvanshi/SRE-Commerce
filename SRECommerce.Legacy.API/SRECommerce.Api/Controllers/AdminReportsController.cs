using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Infrastructure.Data;
using SRECommerce.Domain.Entities;
using SRECommerce.Application.DTOs;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/admin/reports")]
[Authorize(Roles = Roles.Staff)]
public class AdminReportsController(AppDbContext db) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<AdminSummaryDto>> Summary()
    {
        var orders = db.Orders.AsNoTracking();

        var completedOrders = orders.Where(o => o.Status != OrderStatuses.Cancelled);
        // EF Core 8 SQLite cannot aggregate decimal values. Keep decimal arithmetic
        // in .NET for the legacy provider; other providers retain server aggregation.
        var totalRevenue = db.Database.IsSqlite()
            ? (await completedOrders.Select(o => o.Total).ToListAsync()).Sum()
            : await completedOrders.SumAsync(o => (decimal?)o.Total) ?? 0;

        var orderItems = db.OrderItems
            .AsNoTracking()
            .Where(i => i.Order != null && i.Order.Status != OrderStatuses.Cancelled);
        List<AdminTopProductDto> topProducts;
        if (db.Database.IsSqlite())
        {
            var itemValues = await orderItems.Select(i => new { i.ProductName, i.Quantity, i.LineTotal }).ToListAsync();
            topProducts = itemValues.GroupBy(i => i.ProductName)
                .Select(g => new AdminTopProductDto(g.Key, g.Sum(i => i.Quantity), g.Sum(i => i.LineTotal)))
                .OrderByDescending(p => p.QuantitySold).Take(5).ToList();
        }
        else topProducts = await orderItems
            .GroupBy(i => i.ProductName)
            .Select(g => new AdminTopProductDto(
                g.Key,
                g.Sum(x => x.Quantity),
                g.Sum(x => x.LineTotal)
            ))
            .OrderByDescending(x => x.QuantitySold)
            .Take(5)
            .ToListAsync();

        var recentOrders = await orders
            .OrderByDescending(o => o.CreatedAt)
            .Take(8)
            .Select(o => new AdminRecentOrderDto(
                o.OrderNumber,
                o.CustomerName,
                o.Total,
                o.Status,
                o.CreatedAt
            ))
            .ToListAsync();

        var totalStock = await db.Products.SumAsync(p => (int?)p.StockQty) ?? 0;
        var inventoryValue = db.Database.IsSqlite()
            ? (await db.Products.AsNoTracking().Select(p => new { p.StockQty, p.Price }).ToListAsync()).Sum(p => p.StockQty * p.Price)
            : await db.Products.SumAsync(p => (decimal?)(p.StockQty * p.Price)) ?? 0;

        return Ok(new AdminSummaryDto(
            totalRevenue,
            await orders.CountAsync(),
            await orders.CountAsync(o => o.Status == OrderStatuses.Pending),
            await orders.CountAsync(o => o.Status == OrderStatuses.Delivered),
            await db.Products.CountAsync(),
            await db.Users.CountAsync(u => u.Role == Roles.Customer),
            await db.Users.CountAsync(u => u.Role == Roles.Employee),
            totalStock,
            inventoryValue,
            topProducts,
            recentOrders
        ));
    }
}

