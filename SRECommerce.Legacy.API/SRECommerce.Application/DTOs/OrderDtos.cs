namespace SRECommerce.Application.DTOs;

public record CreateOrderRequest(
    string CustomerName,
    string CustomerEmail,
    string Phone,
    string ShippingAddress,
    List<CreateOrderItemRequest> Items
);

public record CreateOrderItemRequest(int ProductId, int Quantity);

public record OrderDto(
    int Id,
    string OrderNumber,
    string CustomerName,
    string CustomerEmail,
    string Phone,
    string ShippingAddress,
    string Status,
    decimal Subtotal,
    decimal DiscountTotal,
    decimal ShippingFee,
    decimal Total,
    DateTime CreatedAt,
    List<OrderItemDto> Items
);

public record OrderItemDto(
    int ProductId,
    string ProductName,
    string Sku,
    string? ImageUrl,
    decimal UnitPrice,
    decimal Discount,
    int Quantity,
    decimal LineTotal
);

public record AdminSummaryDto(
    decimal TotalRevenue,
    int TotalOrders,
    int PendingOrders,
    int DeliveredOrders,
    int TotalProducts,
    int TotalCustomers,
    int TotalEmployees,
    int TotalStock,
    decimal InventoryValue,
    List<AdminTopProductDto> TopProducts,
    List<AdminRecentOrderDto> RecentOrders
);

public record AdminTopProductDto(string ProductName, int QuantitySold, decimal Revenue);
public record AdminRecentOrderDto(string OrderNumber, string CustomerName, decimal Total, string Status, DateTime CreatedAt);

