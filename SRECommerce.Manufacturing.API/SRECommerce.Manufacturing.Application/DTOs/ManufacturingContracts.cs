using SRECommerce.Manufacturing.Domain;

namespace SRECommerce.Manufacturing.Application;

public sealed record BomMaterial(Guid VariantId, decimal PerUnit, decimal StandardUnitCost);
public sealed record BomInput(Guid Id, string Code, string Name, Guid OutputVariantId, BomMaterial[] Materials, string[] Stages);
public sealed record BomView(Guid Id, string Code, string Name, int Revision, Guid OutputVariantId, string Currency, BomMaterial[] Materials, string[] Stages);
public sealed record ProductionInput(Guid Id, Guid BomId, Guid WarehouseId, decimal Quantity);
public sealed record ProductionMaterial(Guid VariantId, decimal Quantity);
public sealed record ProductionAction(string Version, string Note);
public sealed record QualityInput(string Version, bool Passed, string Note);
public sealed record ProductionFinish(string Version, decimal GoodQuantity, decimal WasteQuantity, decimal OverheadCost, DateTimeOffset? ExpiresAt, string Note);
public sealed record ProductionView(Guid Id, Guid BomId, Guid WarehouseId, Guid OutputVariantId, decimal PlannedQuantity, ProductionState State,
    string[] Stages, int StagesCompleted, bool QualityPassed, bool QualityChecked, bool InputsConsumed, ProductionMaterial[] Materials, string Currency,
    decimal StandardMaterialCost, decimal OverheadCost, decimal GoodQuantity, decimal WasteQuantity, string? OutputLot, string? LastFault,
    DateTimeOffset? ReservationExpiresAt, string Version, DateTimeOffset CreatedAt);
public sealed record ProductionHistoryView(ProductionState State, string Action, string Note, DateTimeOffset CreatedAt);
public sealed record InventoryReservation(Guid Id, int Status, string Version);
