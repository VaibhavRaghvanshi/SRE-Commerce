using System.ComponentModel.DataAnnotations;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Application;
public interface IDomainOwnershipVerifier { Task<IReadOnlyCollection<string>> GetTxtAsync(string host,CancellationToken ct); }
