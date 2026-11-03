using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Manufacturing.Domain;

namespace SRECommerce.Manufacturing.Application;

public sealed class ManufacturingService(IManufacturingStore store, IManufacturingAccess access, IManufacturingInventory inventory, TimeProvider clock)
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(value))));
    private static void Id(Guid id) { if (id == Guid.Empty) throw new BusinessFault("InvalidManufacturingId", 400); }
    private static void Page(int page) { if (page is < 1 or > 1000) throw new BusinessFault("InvalidManufacturingPage", 400); }
    private static string Text(string input, int length)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length > length || input.Any(char.IsControl) || input.Contains('<') || input.Contains('>'))
            throw new BusinessFault("InvalidManufacturingText", 400);
        return input.Trim();
    }
    private static void Quantity(decimal value, bool zeroAllowed = false)
    {
        if (value < 0 || value > 1000000 || !zeroAllowed && value == 0 || decimal.Round(value, 3) != value)
            throw new BusinessFault("InvalidProductionQuantity", 400);
    }
    private static void Cost(decimal value)
    { if (value < 0 || value > 1000000000000m || decimal.Round(value, 4) != value) throw new BusinessFault("InvalidProductionCost", 400); }
    private static BomView View(BillOfMaterials row) => new(row.Id, row.Code, row.Name, row.Revision, row.OutputVariantId, row.Currency,
        ContractJson.Read<BomMaterial[]>(row.Materials), ContractJson.Read<string[]>(row.Stages));
    private static ProductionView View(ProductionOrder row) => new(row.Id, row.BomId, row.WarehouseId, row.OutputVariantId, row.PlannedQuantity,
        row.State, ContractJson.Read<string[]>(row.Stages), row.StagesCompleted, row.QualityPassed, row.QualityChecked, row.InputsConsumed,
        ContractJson.Read<ProductionMaterial[]>(row.Materials), row.Currency, row.StandardMaterialCost, row.OverheadCost,
        row.GoodQuantity, row.WasteQuantity, row.OutputLot, row.LastFault, row.ReservationExpiresAt, Convert.ToBase64String(row.Version), row.CreatedAt);
    private async Task<ProductionOrder> Required(Guid id, CancellationToken ct) => await store.Order(id, ct) ?? throw new BusinessFault("ProductionOrderNotFound", 404);

    public async Task<BomView[]> Boms(Guid tenant, int page, CancellationToken ct)
    { await access.Human(tenant, ActorPermission.Staff, ct); Page(page); return (await store.Boms(page, ct)).Select(View).ToArray(); }
    public async Task<BomView> CreateBom(Guid tenant, BomInput input, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); Id(input.Id); Id(input.OutputVariantId);
        var code = Text(input.Code, 48).ToUpperInvariant(); var name = Text(input.Name, 160);
        if (!Regex.IsMatch(code, @"\A[A-Z0-9][A-Z0-9_-]{0,47}\z") || input.Materials is not { Length: >= 1 and <= 40 } || input.Stages is not { Length: >= 1 and <= 12 } ||
            input.Materials.Select(m => m.VariantId).Distinct().Count() != input.Materials.Length)
            throw new BusinessFault("InvalidBillOfMaterials", 400);
        foreach (var material in input.Materials)
        {
            Id(material.VariantId); Quantity(material.PerUnit); Cost(material.StandardUnitCost);
            if (material.VariantId == input.OutputVariantId) throw new BusinessFault("OutputCannotBeAnInput", 400);
        }
        var stages = input.Stages.Select(s => Text(s, 80)).ToArray();
        if (stages.Distinct(StringComparer.OrdinalIgnoreCase).Count() != stages.Length) throw new BusinessFault("DuplicateProductionStage", 400);
        var materials = ContractJson.Write(input.Materials.OrderBy(m => m.VariantId).ToArray());
        var stageJson = ContractJson.Write(stages); var currency = await access.Currency(tenant, ct);
        return await store.Transaction(async token =>
        {
            if (await store.Bom(input.Id, token) is {} previous)
            {
                if (previous.Code != code || previous.Name != name || previous.OutputVariantId != input.OutputVariantId || previous.Materials != materials || previous.Stages != stageJson)
                    throw new BusinessFault("BomKeyReused");
                return View(previous);
            }
            var row = new BillOfMaterials { TenantId = tenant, Id = input.Id, Code = code, Name = name, Revision = 1 + await store.Revision(code, token),
                OutputVariantId = input.OutputVariantId, Currency = currency, Materials = materials, Stages = stageJson, CreatedAt = Now };
            store.Add(row); await store.Save("manufacturing.bom.created", row.Id, token); return View(row);
        }, ct);
    }

    public async Task<ProductionView[]> Orders(Guid tenant, int page, CancellationToken ct)
    { await access.Human(tenant, ActorPermission.Staff, ct); Page(page); return (await store.Orders(page, ct)).Select(View).ToArray(); }
    public async Task<ProductionView> Get(Guid tenant, Guid id, CancellationToken ct)
    { await access.Human(tenant, ActorPermission.Staff, ct); return View(await Required(id, ct)); }
    public async Task<ProductionHistoryView[]> History(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Staff, ct); await Required(id, ct);
        return (await store.History(id, ct)).Select(x => new ProductionHistoryView(x.State, x.Action, x.Note, x.CreatedAt)).ToArray();
    }
    public async Task<ProductionView> Create(Guid tenant, ProductionInput input, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); Id(input.Id); Id(input.BomId); Id(input.WarehouseId); Quantity(input.Quantity);
        var hash = Hash(input);
        if (await store.Order(input.Id, ct) is {} prior) return Replay(prior, hash);
        var bom = await store.Bom(input.BomId, ct) ?? throw new BusinessFault("BomNotFound", 404);
        var definitions = ContractJson.Read<BomMaterial[]>(bom.Materials);
        var quantities = definitions.Select(m => new ProductionMaterial(m.VariantId, m.PerUnit * input.Quantity)).ToArray();
        foreach (var line in quantities) Quantity(line.Quantity);
        var estimate = decimal.Round(definitions.Sum(x => x.StandardUnitCost * x.PerUnit * input.Quantity), 4, MidpointRounding.ToEven); Cost(estimate);
        await inventory.Validate(tenant, input.WarehouseId, quantities.Select(m => m.VariantId).Append(bom.OutputVariantId).ToArray(), ct);
        return await store.Transaction(async token =>
        {
            if (await store.Order(input.Id, token) is {} duplicate) return Replay(duplicate, hash);
            var row = new ProductionOrder { TenantId = tenant, Id = input.Id, BomId = bom.Id, WarehouseId = input.WarehouseId, OutputVariantId = bom.OutputVariantId,
                PlannedQuantity = input.Quantity, Materials = ContractJson.Write(quantities), Stages = bom.Stages, Currency = bom.Currency,
                StandardMaterialCost = estimate, RequestHash = hash, CreatedAt = Now, NextAttemptAt = Now };
            store.Add(row); await Record(row, "planned", "Production planned", token); return View(row);
        }, ct);
    }
    private static ProductionView Replay(ProductionOrder row, string hash) => row.RequestHash == hash ? View(row) : throw new BusinessFault("ProductionKeyReused");

    private async Task Record(ProductionOrder row, string action, string note, CancellationToken ct)
    {
        store.Add(new ProductionHistory { Id = Guid.NewGuid(), TenantId = row.TenantId, OrderId = row.Id,
            ActorId = access.Actor, State = row.State, Action = action, Note = note, CreatedAt = Now });
        await store.Save("manufacturing.production." + action, row.Id, ct);
    }
    private async Task<ProductionView> Change(Guid tenant, Guid id, string version, string note, string action, ActorPermission permission, Action<ProductionOrder> change, CancellationToken ct)
    {
        await access.Human(tenant, permission, ct); note = Text(note, 300);
        return await store.Transaction(async token =>
        {
            var row = await Required(id, token); store.Version(row, version);
            if (row.LeaseUntil > Now) throw new BusinessFault("ProductionWorkInProgress");
            change(row); await Record(row, action, note, token); return View(row);
        }, ct);
    }
    public Task<ProductionView> Start(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) =>
        Change(tenant, id, input.Version, input.Note, "start-requested", ActorPermission.Manager, row =>
        {
            if (row.State != ProductionState.Planned) throw new BusinessFault("ProductionAlreadyStarted");
            row.ReservationExpiresAt = Now.AddMinutes(45); row.State = ProductionState.Reserving; row.NextAttemptAt = Now;
        }, ct);
    public Task<ProductionView> Stage(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) =>
        Change(tenant, id, input.Version, input.Note, "stage-completed", ActorPermission.Staff, row =>
        {
            if (row.State != ProductionState.Running || row.StagesCompleted >= ContractJson.Read<string[]>(row.Stages).Length) throw new BusinessFault("NoPendingProductionStage");
            row.StagesCompleted++;
        }, ct);
    public Task<ProductionView> Quality(Guid tenant, Guid id, QualityInput input, CancellationToken ct) =>
        Change(tenant, id, input.Version, input.Note, input.Passed ? "quality-passed" : "quality-failed", ActorPermission.Staff, row =>
        {
            if (row.State != ProductionState.Running || row.StagesCompleted != ContractJson.Read<string[]>(row.Stages).Length) throw new BusinessFault("CompleteStagesBeforeQuality");
            row.QualityChecked = true; row.QualityPassed = input.Passed;
        }, ct);
    public Task<ProductionView> Finish(Guid tenant, Guid id, ProductionFinish input, CancellationToken ct) =>
        Change(tenant, id, input.Version, input.Note, "output-recorded", ActorPermission.Manager, row =>
        {
            Quantity(input.GoodQuantity, true); Quantity(input.WasteQuantity, true); Cost(input.OverheadCost);
            if (row.State != ProductionState.Running || !row.InputsConsumed || !row.QualityChecked ||
                input.GoodQuantity > 0 && !row.QualityPassed || input.GoodQuantity + input.WasteQuantity != row.PlannedQuantity ||
                input.ExpiresAt <= Now || input.GoodQuantity == 0 && input.ExpiresAt is not null)
                throw new BusinessFault("InvalidProductionOutput");
            row.GoodQuantity = input.GoodQuantity; row.WasteQuantity = input.WasteQuantity; row.OverheadCost = input.OverheadCost;
            row.OutputExpiresAt = input.ExpiresAt; row.OutputLot = input.GoodQuantity == 0 ? null : "PROD-" + row.Id.ToString("N").ToUpperInvariant();
            row.State = input.GoodQuantity == 0 ? ProductionState.Completed : ProductionState.Receiving; row.NextAttemptAt = Now;
        }, ct);
    public Task<ProductionView> Cancel(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) =>
        Change(tenant, id, input.Version, input.Note, "cancel-requested", ActorPermission.Manager, row =>
        {
            if (row.InputsConsumed || !(row.State is ProductionState.Planned or ProductionState.Reserving ||
                row.State == ProductionState.NeedsReview && row.ResumeState == ProductionState.Reserving)) throw new BusinessFault("CannotCancelProduction");
            row.State = row.State == ProductionState.Planned ? ProductionState.Cancelled : ProductionState.Cancelling;
            row.NextAttemptAt = Now; row.Attempts = 0; row.LastFault = null; row.ResumeState = null;
        }, ct);
    public Task<ProductionView> Retry(Guid tenant, Guid id, ProductionAction input, CancellationToken ct) =>
        Change(tenant, id, input.Version, input.Note, "recovery-requested", ActorPermission.Manager, row =>
        {
            if (row.State != ProductionState.NeedsReview || row.ResumeState is not {} previous) throw new BusinessFault("ProductionDoesNotNeedReview");
            row.State = previous; row.ResumeState = null; row.Attempts = 0; row.LastFault = null; row.NextAttemptAt = Now;
        }, ct);
}
