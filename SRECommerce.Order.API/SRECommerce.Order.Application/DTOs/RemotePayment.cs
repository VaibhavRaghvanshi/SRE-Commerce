using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record RemotePayment(Guid Id,Guid OrderId,string Currency,decimal Amount,string Method,int Status,decimal Refunded,string Version);
