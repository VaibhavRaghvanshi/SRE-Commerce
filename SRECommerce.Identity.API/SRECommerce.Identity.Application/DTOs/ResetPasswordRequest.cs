using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public record ResetPasswordRequest([Required, EmailAddress] string Email, [Required] string Code, [Required, StringLength(72, MinimumLength=12)] string Password);
