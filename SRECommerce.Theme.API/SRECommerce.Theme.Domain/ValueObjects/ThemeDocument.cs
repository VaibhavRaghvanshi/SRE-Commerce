using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;
public sealed record ThemeDocument(int SchemaVersion,ThemePalette Light,ThemePalette Dark,DeviceTypography Desktop,DeviceTypography Mobile,string DefaultMode);
