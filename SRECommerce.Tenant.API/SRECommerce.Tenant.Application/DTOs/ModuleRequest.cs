using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record ModuleRequest([Required,MaxLength(24)]string Version,bool Enabled);
