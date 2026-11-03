using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class ShippingZone : ITenantRecord, IVersionedRecord
{
 public Guid TenantId {get;set;} public Guid Id {get;set;}=Guid.NewGuid();
 public string Code {get;set;}=""; public string Name {get;set;}="";
 public string Countries {get;set;}="[]"; public string PostalPrefixes {get;set;}="[]";
 public bool Active {get;set;}=true; public byte[] Version {get;set;}=[];
}
