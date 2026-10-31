using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Pricing.Domain;
public sealed class CustomerGroupMember:ITenantRecord,IVersionedRecord
{
    public Guid TenantId {get;set;}
    public Guid Id {get;set;}=Guid.NewGuid();
    public Guid UserId {get;set;} public Guid GroupId {get;set;} public bool Active {get;set;}=true;
    public byte[] Version {get;set;}=[];
}
