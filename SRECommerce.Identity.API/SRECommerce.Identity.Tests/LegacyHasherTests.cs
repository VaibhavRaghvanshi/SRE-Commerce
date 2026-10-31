using Microsoft.AspNetCore.Identity;
using SRECommerce.Identity.Infrastructure;
using Xunit;

namespace SRECommerce.Identity.Tests;

public sealed class LegacyHasherTests
{
    [Fact]
    public void Valid_tagged_bcrypt_requests_native_rehash()
    {
        var password=Protocol.Password();
        var user=new ApplicationUser{LegacyPasswordHash=BCrypt.Net.BCrypt.HashPassword(password,4)};
        Assert.Equal(PasswordVerificationResult.SuccessRehashNeeded,new LegacyPasswordHasher().VerifyHashedPassword(user,"LEGACY_BCRYPT",password));
        Assert.Null(user.LegacyPasswordHash);
    }
    [Fact]
    public void Wrong_bcrypt_password_cannot_clear_the_migration_marker()
    {
        var user=new ApplicationUser{LegacyPasswordHash=BCrypt.Net.BCrypt.HashPassword(Protocol.Password(),4)};
        Assert.Equal(PasswordVerificationResult.Failed,new LegacyPasswordHasher().VerifyHashedPassword(user,"LEGACY_BCRYPT","wrong"));
        Assert.NotNull(user.LegacyPasswordHash);
    }
    [Fact]
    public void Native_password_never_falls_back_to_old_bcrypt()
    {
        var old=Protocol.Password();
        var user=new ApplicationUser{LegacyPasswordHash=BCrypt.Net.BCrypt.HashPassword(old,4)};
        var hasher=new LegacyPasswordHasher();
        var native=hasher.HashPassword(user,Protocol.Password());
        Assert.Equal(PasswordVerificationResult.Failed,hasher.VerifyHashedPassword(user,native,old));
    }
    [Fact]
    public void Bcrypt_truncation_cannot_authenticate_a_longer_password()
    {
        var prefix=new string('a',72);
        var user=new ApplicationUser{LegacyPasswordHash=BCrypt.Net.BCrypt.HashPassword(prefix,4)};
        Assert.Equal(PasswordVerificationResult.Failed,new LegacyPasswordHasher().VerifyHashedPassword(user,"LEGACY_BCRYPT",prefix+"suffix"));
        Assert.NotNull(user.LegacyPasswordHash);
    }
}
