using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class ReservationLine:ITenantRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid ReservationId {get;set;} public Guid BalanceId {get;set;} public Guid VariantId {get;set;} public decimal Quantity {get;set;}
}
