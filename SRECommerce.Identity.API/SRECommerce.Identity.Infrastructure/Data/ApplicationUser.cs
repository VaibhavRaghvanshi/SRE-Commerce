using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Infrastructure;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string? LegacyPasswordHash { get; set; }
}
