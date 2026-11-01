using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderOperation:ITenantRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public Guid OrderId {get;set;}public string Action {get;set;}="";public string Hash {get;set;}="";public string Result {get;set;}="";public DateTimeOffset CreatedAt {get;set;}
}
