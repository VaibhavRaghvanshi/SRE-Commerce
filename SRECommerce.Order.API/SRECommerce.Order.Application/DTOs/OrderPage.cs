using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record OrderPage(OrderView[] Items,int Total,int Page,int PageSize);
