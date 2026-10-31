using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record SaveTemplate(string? Version,[Required]LayoutDocument Document);
