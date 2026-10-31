using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SRECommerce.Domain.Common;
using SRECommerce.Infrastructure.Data;
using SRECommerce.Domain.Entities;
using SRECommerce.Application.DTOs;

namespace SRECommerce.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class OrdersController(AppDbContext db) : ControllerBase
{
    [HttpGet("mine")]
    public async Task<ActionResult<List<OrderDto>>> Mine()
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var orders = await db.Orders
            .Include(o => o.Items)
            .Where(o => o.UserId == userId.Value)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        return Ok(orders.Select(ToDto).ToList());
    }

    [HttpGet("admin")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<List<OrderDto>>> AdminList()
    {
        var orders = await db.Orders
            .Include(o => o.Items)
            .OrderByDescending(o => o.CreatedAt)
            .Take(100)
            .ToListAsync();

        return Ok(orders.Select(ToDto).ToList());
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Cart is empty." });
        }

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var order = new Order
        {
            UserId = userId.Value,
            OrderNumber = $"SRE-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}",
            CustomerName = request.CustomerName.Trim(),
            CustomerEmail = request.CustomerEmail.Trim(),
            Phone = request.Phone.Trim(),
            ShippingAddress = request.ShippingAddress.Trim(),
            Status = OrderStatuses.Pending,
            ShippingFee = 99
        };

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0) continue;
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return BadRequest(new { message = $"Product {item.ProductId} was not found." });
            }

            if (product.StockQty < item.Quantity)
            {
                return BadRequest(new { message = $"{product.Name} has only {product.StockQty} in stock." });
            }

            var discount = product.Discount ?? 0;
            var unitAfterDiscount = Math.Max(0, product.Price - discount);
            var lineTotal = unitAfterDiscount * item.Quantity;

            product.StockQty -= item.Quantity;
            order.Subtotal += product.Price * item.Quantity;
            order.DiscountTotal += discount * item.Quantity;
            order.Total += lineTotal;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                Sku = product.Sku,
                ImageUrl = product.ImageUrl,
                UnitPrice = product.Price,
                Discount = discount,
                Quantity = item.Quantity,
                LineTotal = lineTotal
            });
        }

        if (order.Items.Count == 0)
        {
            return BadRequest(new { message = "Cart is empty." });
        }

        order.Total += order.ShippingFee;
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(Mine), new { id = order.Id }, ToDto(order));
    }

    private int? GetUserId()
    {
        var value = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : null;
    }

    private static OrderDto ToDto(Order o) => new(
        o.Id,
        o.OrderNumber,
        o.CustomerName,
        o.CustomerEmail,
        o.Phone,
        o.ShippingAddress,
        o.Status,
        o.Subtotal,
        o.DiscountTotal,
        o.ShippingFee,
        o.Total,
        o.CreatedAt,
        o.Items.Select(i => new OrderItemDto(
            i.ProductId,
            i.ProductName,
            i.Sku,
            i.ImageUrl,
            i.UnitPrice,
            i.Discount,
            i.Quantity,
            i.LineTotal
        )).ToList()
    );
}

