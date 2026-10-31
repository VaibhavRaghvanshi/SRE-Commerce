using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public record AccountView(Guid Id, string Email, string FullName, AccountKind Kind, bool EmailConfirmed, bool MfaEnabled);
