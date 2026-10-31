using System.ComponentModel.DataAnnotations;
namespace SRECommerce.Catalog.Application;
public record CategoryRequest([Required]string Name,[Required]string Slug,Guid? ParentId,string? Version=null,bool Archived=false);
