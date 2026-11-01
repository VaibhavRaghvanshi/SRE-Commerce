using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Payment.Infrastructure;
using Xunit;
namespace SRECommerce.Payment.Tests;
public sealed class PaymentSignatureTests
{
    [Fact] public void Missing_signing_key_fails_closed()
    {
        var signer = new PaymentSignatures(new ConfigurationBuilder().Build());
        Assert.Throws<DependencyFault>(() => signer.Sign(Guid.NewGuid(), "1", "{}"));
    }
    [Fact] public void Exact_body_and_tenant_are_covered_and_rotated_keys_invalidate_old_signatures()
    {
        PaymentSignatures NewSigner() => new(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Sandbox:WebhookSigningKey"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)) }).Build());
        var signer = NewSigner(); var tenant = Guid.NewGuid(); var now = DateTimeOffset.UtcNow; var stamp = now.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signature = signer.Sign(tenant, stamp, "{}"); signer.Verify(tenant, stamp, "{}", signature, now);
        Assert.Throws<BusinessFault>(() => signer.Verify(tenant, stamp, "{ }", signature, now));
        Assert.Throws<BusinessFault>(() => NewSigner().Verify(tenant, stamp, "{}", signature, now));
    }
}
