using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Order.Domain;
public sealed class OrderPolicy:ITenantRecord,IVersionedRecord
{
 public Guid TenantId {get;set;}public Guid Id {get;set;}public bool AllowGuest {get;set;}=true;public bool AllowCOD {get;set;}public bool AllowExchanges {get;set;}=true;public int ReturnWindowDays {get;set;}=14;public decimal MaximumTotal {get;set;}=1000000000m;public byte[] Version {get;set;}=[];
}
