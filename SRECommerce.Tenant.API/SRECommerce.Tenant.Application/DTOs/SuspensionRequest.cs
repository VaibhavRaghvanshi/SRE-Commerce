using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record SuspensionRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(300)]string Reason);
