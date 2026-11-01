using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Inventory.Domain;
public sealed class ReservationCancellation:ITenantRecord
{
    public Guid TenantId {get;set;}public Guid Id {get;set;}public string SourceService {get;set;}="";public Guid OperationId {get;set;}public DateTimeOffset CreatedAt {get;set;}
}
