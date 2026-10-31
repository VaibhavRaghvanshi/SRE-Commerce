using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record SaveTheme([Required]string Version,[Required]ThemeDocument Document);
