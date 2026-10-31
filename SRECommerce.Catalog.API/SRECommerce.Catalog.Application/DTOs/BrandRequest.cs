using System.ComponentModel.DataAnnotations;
namespace SRECommerce.Catalog.Application;
public record BrandRequest([Required]string Name,[Required]string Slug,string? Version=null,bool Archived=false);
