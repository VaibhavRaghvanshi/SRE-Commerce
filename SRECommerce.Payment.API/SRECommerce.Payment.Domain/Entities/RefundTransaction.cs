using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Payment.Domain;
public sealed class RefundTransaction : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Reason { get; set; } = "";
    public string ProviderReference { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
