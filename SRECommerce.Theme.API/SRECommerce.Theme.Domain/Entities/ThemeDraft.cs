using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;
public sealed class ThemeDraft:ITenantRecord,IVersionedRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public string Name{get;set;}="";public string Document{get;set;}="";
    public bool Archived{get;set;}public DateTimeOffset UpdatedAt{get;set;}=DateTimeOffset.UtcNow;public byte[] Version{get;set;}=[];
}
