using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record RegistrationRequest([Required,StringLength(160)]string Name,[Required,StringLength(63)]string Slug,[Required,StringLength(40)]string BusinessTypeCode,[StringLength(100)]string? CustomBusinessType);
