using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;
public sealed class TenantTheme:ITenantRecord,IVersionedRecord
{
    public Guid TenantId{get;set;}public Guid ThemeVersionId{get;set;}public long Sequence{get;set;}public byte[] Version{get;set;}=[];
}
