using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public record HeaderOptions(bool ShowSearch,bool ShowCategories,bool ShowCart,bool Sticky);
