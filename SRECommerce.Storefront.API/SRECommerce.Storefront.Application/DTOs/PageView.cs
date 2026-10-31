using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record PageView(Guid Id,string Slug,string Title,string Version,LayoutDocument Document);
