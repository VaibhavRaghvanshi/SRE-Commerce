using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public sealed class LayoutTemplate:IVersionedRecord
{
    public string BusinessType{get;set;}="neutral";public string Document{get;set;}="";public byte[] Version{get;set;}=[];public DateTimeOffset UpdatedAt{get;set;}=DateTimeOffset.UtcNow;
}
