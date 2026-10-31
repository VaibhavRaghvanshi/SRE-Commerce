using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record SavePreset(string? Version,[Required]ThemeDocument Document);
