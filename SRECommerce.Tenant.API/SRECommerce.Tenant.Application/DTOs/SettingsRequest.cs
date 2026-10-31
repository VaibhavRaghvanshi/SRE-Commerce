using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record SettingsRequest([Required,MaxLength(24)]string Version,[Required,StringLength(3,MinimumLength=3)]string Currency,[Required,MaxLength(20)]string Culture,[Required,MaxLength(100)]string TimeZone,[Required,MaxLength(160)]string DisplayName,[MaxLength(500)]string? LogoUrl);
