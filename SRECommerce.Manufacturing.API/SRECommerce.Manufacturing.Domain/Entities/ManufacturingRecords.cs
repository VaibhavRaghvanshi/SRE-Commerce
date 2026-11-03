using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Manufacturing.Domain;

public sealed class BillOfMaterials : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public int Revision { get; set; }
    public Guid OutputVariantId { get; set; }
    public string Name { get; set; } = "";
    public string Currency { get; set; } = "";
    public string Materials { get; set; } = "[]";
    public string Stages { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
}

public enum ProductionState { Planned, Reserving, Running, Consuming, Receiving, Completed, Cancelling, Cancelled, NeedsReview }
public sealed class ProductionOrder : ITenantRecord, IVersionedRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid BomId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid OutputVariantId { get; set; }
    public decimal PlannedQuantity { get; set; }
    public string Materials { get; set; } = "[]";
    public string Stages { get; set; } = "[]";
    public string Currency { get; set; } = "";
    public decimal StandardMaterialCost { get; set; }
    public decimal OverheadCost { get; set; }
    public decimal GoodQuantity { get; set; }
    public decimal WasteQuantity { get; set; }
    public string? OutputLot { get; set; }
    public DateTimeOffset? OutputExpiresAt { get; set; }
    public DateTimeOffset? ReservationExpiresAt { get; set; }
    public int StagesCompleted { get; set; }
    public bool QualityPassed { get; set; }
    public bool QualityChecked { get; set; }
    public bool InputsConsumed { get; set; }
    public ProductionState State { get; set; }
    public ProductionState? ResumeState { get; set; }
    public int Attempts { get; set; }
    public string? LastFault { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseId { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string RequestHash { get; set; } = "";
    public byte[] Version { get; set; } = [];
}

public sealed class ProductionHistory : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid? ActorId { get; set; }
    public ProductionState State { get; set; }
    public string Action { get; set; } = "";
    public string Note { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}
