using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record VersionRequest([Required,MaxLength(24)]string Version);
