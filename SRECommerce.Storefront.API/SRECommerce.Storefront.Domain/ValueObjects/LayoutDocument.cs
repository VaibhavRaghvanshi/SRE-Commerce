using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public record LayoutDocument(int SchemaVersion,string Template,HeaderOptions Header,FooterOptions Footer,NavigationMenu[] Menus,Banner[] Banners,LayoutSection[] Sections,ProductVisibility Product);
