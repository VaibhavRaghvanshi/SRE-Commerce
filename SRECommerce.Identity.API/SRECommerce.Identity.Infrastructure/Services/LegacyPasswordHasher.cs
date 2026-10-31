using System.Net;
using System.Net.Mail;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using SRECommerce.Identity.Application;
using SRECommerce.Identity.Domain;

namespace SRECommerce.Identity.Infrastructure;

public sealed class LegacyPasswordHasher : PasswordHasher<ApplicationUser>
{
    public override PasswordVerificationResult VerifyHashedPassword(ApplicationUser user, string hashedPassword, string providedPassword)
    {
        if (user.LegacyPasswordHash is not null && hashedPassword == "LEGACY_BCRYPT")
        {
            if (System.Text.Encoding.UTF8.GetByteCount(providedPassword)>72) return PasswordVerificationResult.Failed;
            try
            {
                if (BCrypt.Net.BCrypt.Verify(providedPassword, user.LegacyPasswordHash))
                {
                    user.LegacyPasswordHash = null;
                    return PasswordVerificationResult.SuccessRehashNeeded;
                }
            }
            catch (BCrypt.Net.SaltParseException) { }
            return PasswordVerificationResult.Failed;
        }
        return base.VerifyHashedPassword(user, hashedPassword, providedPassword);
    }
}
