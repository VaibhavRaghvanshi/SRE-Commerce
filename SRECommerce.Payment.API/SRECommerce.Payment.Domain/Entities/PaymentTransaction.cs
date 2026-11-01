using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Payment.Domain;
public sealed class PaymentTransaction : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? UserId { get; set; }
    public string Currency { get; set; } = "";
    public int DecimalPlaces { get; set; } = 2;
    public decimal Amount { get; set; }
    public decimal Refunded { get; set; }
    public string Method { get; set; } = "";
    public string Provider { get; set; } = "Sandbox";
    public string ProviderReference { get; set; } = "";
    public PaymentStatus Status { get; set; }
    public bool CancellationRequested { get; set; }
    public string OrderProof { get; set; } = "{}";
    public int Revision { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
