using System.ComponentModel.DataAnnotations;
using SRECommerce.Theme.Domain;

namespace SRECommerce.Theme.Application;
public record CreateTheme([Required,MaxLength(160)]string Name,[MaxLength(40)]string Preset="neutral");
