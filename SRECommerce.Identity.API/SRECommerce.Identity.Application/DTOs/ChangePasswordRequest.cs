using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public record ChangePasswordRequest([Required] string CurrentPassword, [Required, StringLength(72, MinimumLength=12)] string NewPassword);
