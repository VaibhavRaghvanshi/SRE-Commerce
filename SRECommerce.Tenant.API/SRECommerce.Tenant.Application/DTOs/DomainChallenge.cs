using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public record DomainChallenge(Guid Id,string Host,string RecordName,string Token,DateTimeOffset ExpiresAt,string Version);
