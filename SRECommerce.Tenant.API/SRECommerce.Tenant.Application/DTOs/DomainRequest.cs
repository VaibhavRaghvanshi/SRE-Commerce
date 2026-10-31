using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record DomainRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(253)]string Host);
