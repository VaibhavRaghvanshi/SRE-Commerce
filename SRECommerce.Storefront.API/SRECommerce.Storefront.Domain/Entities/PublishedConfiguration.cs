using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public sealed class PublishedConfiguration:ITenantRecord,IVersionedRecord
{
    public Guid TenantId{get;set;}public Guid PageId{get;set;}public Guid LayoutVersionId{get;set;}public long Sequence{get;set;}public byte[] Version{get;set;}=[];
}
