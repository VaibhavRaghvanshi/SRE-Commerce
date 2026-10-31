using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public record FooterOptions(string Copyright,bool ShowContact,string? ContactText);
