using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public record RegistrationRequest([Required, EmailAddress, MaxLength(254)] string Email, [Required, StringLength(72, MinimumLength=12)] string Password, [Required, StringLength(160, MinimumLength=1)] string FullName, AccountKind Kind = AccountKind.Customer);
