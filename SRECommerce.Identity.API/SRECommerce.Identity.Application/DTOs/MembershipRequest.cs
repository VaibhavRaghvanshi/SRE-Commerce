using System.ComponentModel.DataAnnotations;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Application;
public record MembershipRequest(Guid UserId, [Required] string Role);
