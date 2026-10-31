using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record SavePage([Required]string Version,[Required,MaxLength(160)]string Title,[Required]LayoutDocument Document);
