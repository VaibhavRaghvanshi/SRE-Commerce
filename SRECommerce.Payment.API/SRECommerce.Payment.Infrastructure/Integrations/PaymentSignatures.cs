using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Payment.Application;
namespace SRECommerce.Payment.Infrastructure;
public sealed class PaymentSignatures(IConfiguration config) : IPaymentSignatures
{
    private byte[] Key()
    {
        var key = config["Sandbox:WebhookSigningKey"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32) throw new DependencyFault("SandboxWebhookSigningKey");
        return Encoding.UTF8.GetBytes(key);
    }
    public string Sign(Guid tenant, string timestamp, string document) => Convert.ToHexString(HMACSHA256.HashData(Key(),
        Encoding.UTF8.GetBytes(tenant.ToString("N") + "." + timestamp + "." + document)));
    public void Verify(Guid tenant, string timestamp, string document, string signature, DateTimeOffset now)
    {
        if (tenant == Guid.Empty || !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds)
            || seconds < now.ToUnixTimeSeconds() - 300 || seconds > now.ToUnixTimeSeconds() + 300 || signature.Length != 64) throw new BusinessFault("InvalidWebhookSignature", 401);
        byte[] supplied; try { supplied = Convert.FromHexString(signature); } catch (FormatException) { throw new BusinessFault("InvalidWebhookSignature", 401); }
        if (!CryptographicOperations.FixedTimeEquals(supplied, Convert.FromHexString(Sign(tenant, timestamp, document)))) throw new BusinessFault("InvalidWebhookSignature", 401);
    }
}
