using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record PlanRequest([Required,MaxLength(40)]string Code,[Required,MaxLength(100)]string Name,[Range(1,100)]int MaxLocations,[Range(0,50)]int MaxDomains,[Range(1,100000)]int MaxUsers,[Required,MaxLength(12)]string[] Modules);
