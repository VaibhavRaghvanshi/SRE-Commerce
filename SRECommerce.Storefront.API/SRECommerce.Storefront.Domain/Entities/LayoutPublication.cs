using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public sealed class LayoutPublication:ITenantRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public Guid PageId{get;set;}public Guid LayoutVersionId{get;set;}public long Sequence{get;set;}public bool Rollback{get;set;}public DateTimeOffset PublishedAt{get;set;}=DateTimeOffset.UtcNow;
}
