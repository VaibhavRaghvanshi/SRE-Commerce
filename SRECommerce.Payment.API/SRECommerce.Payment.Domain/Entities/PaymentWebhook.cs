using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Payment.Domain;
public sealed class PaymentWebhook : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public string PayloadHash { get; set; } = "";
    public string Outcome { get; set; } = "";
    public bool Applied { get; set; }
    public DateTimeOffset ProviderTimestamp { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
