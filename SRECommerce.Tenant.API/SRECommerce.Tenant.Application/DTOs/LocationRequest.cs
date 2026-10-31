using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record LocationRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(160)]string Name,[Required,MaxLength(500)]string Address,[Required,StringLength(2,MinimumLength=2)]string CountryCode);
