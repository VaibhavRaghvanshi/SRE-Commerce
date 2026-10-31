using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public record Banner(Guid Id,string Title,string ImageUrl,string? Alt,string Link,DevicePlacement Desktop,DevicePlacement Mobile);
