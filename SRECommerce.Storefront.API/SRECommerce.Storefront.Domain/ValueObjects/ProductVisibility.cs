using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public record ProductVisibility(bool Reviews,bool Description,bool Specifications,string[] BusinessFields);
