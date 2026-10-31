using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;

public sealed record ThemePalette(Dictionary<string,string> Colors);
