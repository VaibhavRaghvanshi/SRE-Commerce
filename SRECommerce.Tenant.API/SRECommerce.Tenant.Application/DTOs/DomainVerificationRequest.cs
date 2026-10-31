using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record DomainVerificationRequest([Required,MaxLength(24)]string Version,[Required,MaxLength(100)]string Token,bool MakePrimary=false);
