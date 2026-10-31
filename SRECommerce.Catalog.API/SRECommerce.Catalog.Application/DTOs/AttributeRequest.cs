using System.ComponentModel.DataAnnotations;
using SRECommerce.Catalog.Domain;
namespace SRECommerce.Catalog.Application;
public record AttributeRequest([Required]string Code,[Required]string Name,AttributeDataType DataType,string[] Options,string? Unit=null,string? Version=null,bool Archived=false);
