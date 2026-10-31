using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;
public sealed record DeviceTypography(string Font,int FontSize,int Radius,string Spacing,decimal HeadingScale=1.25m,decimal LineHeight=1.5m);
