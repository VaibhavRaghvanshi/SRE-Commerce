using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public record ConfirmEmailRequest([Required, EmailAddress] string Email, [Required] string Code);
