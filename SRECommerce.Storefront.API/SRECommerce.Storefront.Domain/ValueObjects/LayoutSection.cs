using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public record LayoutSection(Guid Id,string Kind,string Title,string? Text,Guid[] References,DevicePlacement Desktop,DevicePlacement Mobile);
