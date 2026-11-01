using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderStatusHistory:ITenantRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}=Guid.NewGuid();public Guid OrderId {get;set;}public OrderStatus? Previous {get;set;}public OrderStatus Current {get;set;}public string Reason {get;set;}="";public DateTimeOffset CreatedAt {get;set;}
}
