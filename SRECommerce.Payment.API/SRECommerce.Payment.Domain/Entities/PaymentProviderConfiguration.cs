using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Payment.Domain;
public sealed class PaymentProviderConfiguration : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public bool Active { get; set; } = true;
    public bool AllowCard { get; set; } = true;
    public bool AllowUPI { get; set; } = true;
    public bool AllowNetBanking { get; set; } = true;
    public bool AllowCOD { get; set; }
    public SandboxOutcome Outcome { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] Version { get; set; } = [];
}
