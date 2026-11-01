using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Payment.Domain;
public sealed class PaymentAttempt : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PaymentId { get; set; }
    public int Sequence { get; set; }
    public string Action { get; set; } = "";
    public PaymentStatus? Previous { get; set; }
    public PaymentStatus Current { get; set; }
    public string ProviderReference { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
