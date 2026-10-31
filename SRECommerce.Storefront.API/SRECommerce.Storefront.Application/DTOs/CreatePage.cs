using System.ComponentModel.DataAnnotations;
using SRECommerce.Storefront.Domain;

namespace SRECommerce.Storefront.Application;
public record CreatePage([Required,MaxLength(80)]string Slug,[Required,MaxLength(160)]string Title,[MaxLength(40)]string Template="neutral");
