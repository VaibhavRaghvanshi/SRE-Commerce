using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record ThemeView(Guid Id,string Name,bool Archived,string Version,ThemeDocument Document);
