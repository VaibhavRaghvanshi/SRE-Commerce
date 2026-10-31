using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public sealed class LayoutVersion:ITenantRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public Guid PageId{get;set;}public int Revision{get;set;}public string Document{get;set;}="";public string Hash{get;set;}="";public DateTimeOffset CreatedAt{get;set;}=DateTimeOffset.UtcNow;
}
