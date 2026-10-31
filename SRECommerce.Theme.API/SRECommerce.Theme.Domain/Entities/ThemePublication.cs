using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;
public sealed class ThemePublication:ITenantRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public Guid ThemeVersionId{get;set;}public long Sequence{get;set;}public bool Rollback{get;set;}public DateTimeOffset PublishedAt{get;set;}=DateTimeOffset.UtcNow;
}
