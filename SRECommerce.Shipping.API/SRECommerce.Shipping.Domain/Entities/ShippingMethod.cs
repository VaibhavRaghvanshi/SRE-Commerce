using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Shipping.Domain;
public sealed class ShippingMethod : ITenantRecord, IVersionedRecord
{
 public Guid TenantId {get;set;} public Guid Id {get;set;}=Guid.NewGuid(); public Guid ZoneId {get;set;}
 public string Code {get;set;}=""; public string Name {get;set;}=""; public string Description {get;set;}="";
 public DeliveryKind Kind {get;set;} public string Currency {get;set;}="INR";
 public decimal FlatFee {get;set;} public decimal PerUnitFee {get;set;} public decimal TaxRate {get;set;}
 public decimal? FreeAbove {get;set;} public decimal MinimumOrder {get;set;} public decimal? MaximumOrder {get;set;}
 public decimal MaximumUnits {get;set;}=50000; public Guid? PickupWarehouseId {get;set;}
 public bool Active {get;set;}=true; public byte[] Version {get;set;}=[];
}
