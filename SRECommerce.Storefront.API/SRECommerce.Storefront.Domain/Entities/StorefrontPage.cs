using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public sealed class StorefrontPage:ITenantRecord,IVersionedRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public string Slug{get;set;}="home";public string Title{get;set;}="";public string Document{get;set;}="";public DateTimeOffset UpdatedAt{get;set;}=DateTimeOffset.UtcNow;public byte[] Version{get;set;}=[];
}
