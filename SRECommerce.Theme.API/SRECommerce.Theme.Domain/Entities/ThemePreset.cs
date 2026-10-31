using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;
public sealed class ThemePreset:IVersionedRecord
{
    public string BusinessType{get;set;}="neutral";public string Document{get;set;}="";public DateTimeOffset UpdatedAt{get;set;}=DateTimeOffset.UtcNow;public byte[] Version{get;set;}=[];
}
