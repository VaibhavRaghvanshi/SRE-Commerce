using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class ProductPrice:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid PriceListId {get;set;} public Guid ProductId {get;set;} public Guid VariantId {get;set;} public string Sku {get;set;}="";
    public bool IsBundle {get;set;} public Guid TaxPolicyId {get;set;} public decimal MinimumQuantity {get;set;}=1;
    public decimal Mrp {get;set;} public decimal SellingPrice {get;set;} public decimal? CostPrice {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
