using System.Security.Cryptography;
using System.Text;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Shipping.Domain;

namespace SRECommerce.Shipping.Application;

public sealed class ShippingService(IShippingRepository repository, IShippingAccess access, IShippingDependencies dependencies,
    ICourierProvider courier, TimeProvider clock) : IShippingService
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private static string Version(byte[] version) => Convert.ToBase64String(version);
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(value))));
    private static ZoneView View(ShippingZone z) => new(z.Id, z.Code, z.Name, ContractJson.Read<string[]>(z.Countries), ContractJson.Read<string[]>(z.PostalPrefixes), z.Active, Version(z.Version));
    private static MethodView View(ShippingMethod m) => new(m.Id, m.ZoneId, m.Code, m.Name, m.Description, m.Kind, m.Currency,
        m.FlatFee, m.PerUnitFee, m.TaxRate, m.FreeAbove, m.MinimumOrder, m.MaximumOrder, m.MaximumUnits, m.PickupWarehouseId, m.Active, Version(m.Version));
    private static ShipmentView View(Shipment s) => new(s.Id, s.OrderId, s.WarehouseId, s.Kind, s.Provider, s.CourierReference,
        s.Status, s.Items.OrderBy(i => i.VariantId).Select(i => new ShippingQuantity(i.VariantId, i.Quantity)).ToArray(), s.CreatedAt, s.DispatchedAt, s.DeliveredAt, Version(s.Version));
    private static ReturnPickupView View(ReturnPickup p) => new(p.Id, p.ReturnId, p.OrderId, p.WarehouseId, p.WindowFrom, p.WindowTo, p.Status, p.Note, Version(p.Version));
    private async Task<T?> Replay<T>(Guid operation, string action, string hash, CancellationToken ct) where T : class
    {
        ShippingRules.Id(operation); var prior = await repository.Operation(operation, ct);
        if (prior is null) return null;
        if (prior.Action != action || prior.Hash != hash) throw new BusinessFault("ShippingOperationKeyReused");
        return ContractJson.Read<T>(prior.Result);
    }
    private async Task<T> Record<T>(Guid tenant, Guid operation, string action, string hash, Guid resource, Func<T> view, CancellationToken ct, object? integrationEvent = null)
    {
        await repository.Save(action, resource, integrationEvent, ct); var result = view();
        var document = ContractJson.Write(result); if (document.Length > 131072) throw new BusinessFault("ShippingResultTooLarge", 400);
        repository.Add(new ShippingOperation { TenantId = tenant, Id = operation, Action = action, Hash = hash, Result = document, CreatedAt = Now });
        await repository.Save("shipping.operation.recorded", operation, null, ct); return result;
    }
    public async Task<object> Lookups(Guid tenant, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Staff, ct);
        return new { Zones = (await repository.Zones(ct)).Select(View), Methods = (await repository.Methods(ct)).Select(View), Currency = await access.Currency(tenant, ct) };
    }
    public async Task<object> Regions(Guid tenant,CancellationToken ct)
    {
        await access.Public(tenant,false,ct);var activeZones=(await repository.Methods(ct)).Where(m=>m.Active).Select(m=>m.ZoneId).ToHashSet();
        var countries=(await repository.Zones(ct)).Where(z=>z.Active&&activeZones.Contains(z.Id)).SelectMany(z=>ContractJson.Read<string[]>(z.Countries)).Distinct().Order().ToArray();
        return new{Countries=countries,Currency=await access.Currency(tenant,ct)};
    }
    public async Task<ZoneView> Zone(Guid tenant, Guid? id, ZoneRequest request, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Owner, ct); var code = ShippingRules.Code(request.Code); var name = ShippingRules.Text(request.Name);
        if (request.Countries is null || request.Countries.Length is < 1 or > 50 || request.PostalPrefixes is null || request.PostalPrefixes.Length > 100) throw new BusinessFault("InvalidShippingZone", 400);
        var countries = request.Countries.Select(ShippingRules.Country).Distinct().Order().ToArray();
        var prefixes = request.PostalPrefixes.Select(p => { var value = p.Trim().ToUpperInvariant(); if (value.Length is < 1 or > 16 || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c != ' ' && c != '-')) throw new BusinessFault("InvalidPostalPrefix", 400); return value; }).Distinct().Order().ToArray();
        var hash = Hash(new { id, request });
        return await repository.Transaction(async token =>
        {
            var replay = await Replay<ZoneView>(request.OperationId, "shipping.zone.saved", hash, token); if (replay is not null) return replay;
            var zones = await repository.Zones(token); if (id is null && zones.Length >= 100) throw new BusinessFault("ShippingZoneLimit");
            var zone = id is null ? new ShippingZone { TenantId = tenant } : zones.SingleOrDefault(z => z.Id == id) ?? throw new BusinessFault("ShippingZoneNotFound", 404);
            if (id is null) repository.Add(zone); else repository.Version(zone, request.Version ?? "");
            zone.Code = code; zone.Name = name; zone.Countries = ContractJson.Write(countries); zone.PostalPrefixes = ContractJson.Write(prefixes); zone.Active = request.Active;
            return await Record(tenant, request.OperationId, "shipping.zone.saved", hash, zone.Id, () => View(zone), token);
        }, ct);
    }
    public async Task<MethodView> Method(Guid tenant, Guid? id, MethodRequest request, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Owner, ct); ShippingRules.Id(request.ZoneId);
        var code = ShippingRules.Code(request.Code); var name = ShippingRules.Text(request.Name);
        if (request.Description.Length > 500 || request.Description.Any(char.IsControl) || !Enum.IsDefined(request.Kind) || request.Currency != await access.Currency(tenant, ct)) throw new BusinessFault("InvalidShippingMethod", 400);
        foreach (var amount in new[] { request.FlatFee, request.PerUnitFee, request.MinimumOrder, request.FreeAbove ?? 0, request.MaximumOrder ?? 0 }) ShippingRules.Money(amount);
        if (request.TaxRate is < 0 or > 100 || decimal.Round(request.TaxRate, 4) != request.TaxRate || request.MaximumOrder is { } maximum && maximum < request.MinimumOrder || request.MaximumUnits is <= 0 or > 50000 || decimal.Round(request.MaximumUnits, 3) != request.MaximumUnits) throw new BusinessFault("InvalidDeliveryRules", 400);
        if (request.Kind == DeliveryKind.StorePickup)
        {
            if (request.PickupWarehouseId is not { } warehouse || warehouse == Guid.Empty) throw new BusinessFault("PickupWarehouseRequired", 400);
            await dependencies.Warehouse(tenant, warehouse, ct);
        }
        else if (request.PickupWarehouseId is not null) throw new BusinessFault("PickupWarehouseOnlyForPickup", 400);
        var hash = Hash(new { id, request });
        return await repository.Transaction(async token =>
        {
            var replay = await Replay<MethodView>(request.OperationId, "shipping.method.saved", hash, token); if (replay is not null) return replay;
            var zone = (await repository.Zones(token)).SingleOrDefault(z => z.Id == request.ZoneId) ?? throw new BusinessFault("ShippingZoneNotFound", 404);
            if (request.Active && !zone.Active) throw new BusinessFault("ShippingZoneInactive");
            var methods = await repository.Methods(token); if (id is null && methods.Length >= 100) throw new BusinessFault("ShippingMethodLimit");
            var method = id is null ? new ShippingMethod { TenantId = tenant } : methods.SingleOrDefault(m => m.Id == id) ?? throw new BusinessFault("ShippingMethodNotFound", 404);
            if (id is null) repository.Add(method); else repository.Version(method, request.Version ?? "");
            method.ZoneId = zone.Id; method.Code = code; method.Name = name; method.Description = request.Description.Trim(); method.Kind = request.Kind; method.Currency = request.Currency;
            method.FlatFee = request.FlatFee; method.PerUnitFee = request.PerUnitFee; method.TaxRate = request.TaxRate; method.FreeAbove = request.FreeAbove;
            method.MinimumOrder = request.MinimumOrder; method.MaximumOrder = request.MaximumOrder; method.MaximumUnits = request.MaximumUnits; method.PickupWarehouseId = request.PickupWarehouseId; method.Active = request.Active;
            return await Record(tenant, request.OperationId, "shipping.method.saved", hash, method.Id, () => View(method), token);
        }, ct);
    }

    public async Task<ShippingOption[]> Options(Guid tenant, QuoteOptionsRequest request, bool guest, string? cartKey, CancellationToken ct)
    {
        if (guest) await access.Public(tenant, true, ct); else await access.Human(tenant, ActorPermission.Customer, ct);
        ShippingRules.Id(request.Id); ShippingRules.Id(request.PreparationId);
        var country = ShippingRules.Country(request.CountryCode); var postal = ShippingRules.Postal(request.PostalCode);
        var user = guest ? null : access.Actor?.UserId; var role = guest ? null : access.Actor is { IsPlatformAdmin: true } ? "PlatformAdmin" : access.Actor?.TenantRole;
        var preparation = await dependencies.Preparation(tenant, request.PreparationId, user, role, cartKey, ct);
        if (preparation.TenantId != tenant || preparation.Id != request.PreparationId || preparation.UserId != user || preparation.CustomerRole != role || preparation.ExpiresAt <= Now
            || preparation.Quote.TenantId != tenant || preparation.Quote.UserId != user || preparation.Quote.DecimalPlaces is < 0 or > 3 || preparation.Items.Length is < 1 or > 50
            || preparation.Items.Select(i => i.VariantId).Distinct().Count() != preparation.Items.Length) throw new BusinessFault("ShippingCartProofRejected");
        foreach (var item in preparation.Items) { ShippingRules.Id(item.VariantId); ShippingRules.Quantity(item.Quantity); }
        var hash = Hash(new { request.Id, request.PreparationId, Country = country, Postal = postal, UserId = user, Role = role });
        var replay = await Replay<ShippingOption[]>(request.Id, "shipping.quotes.created", hash, ct); if (replay is not null) return replay;
        var zones = (await repository.Zones(ct)).Where(z => z.Active && ShippingRules.Region(ContractJson.Read<string[]>(z.Countries), ContractJson.Read<string[]>(z.PostalPrefixes), country, postal)).ToDictionary(z => z.Id);
        var amount = preparation.Quote.Calculation.Total; ShippingRules.Money(amount); var units = preparation.Items.Sum(i => i.Quantity);
        var methods = (await repository.Methods(ct)).Where(m => m.Active && zones.ContainsKey(m.ZoneId) && m.Currency == preparation.Quote.Currency && amount >= m.MinimumOrder
            && (m.MaximumOrder is null || amount <= m.MaximumOrder) && units <= m.MaximumUnits).OrderBy(m => m.Code, StringComparer.Ordinal).ToArray();
        if (methods.Length > 30) throw new BusinessFault("TooManyDeliveryOptions");
        // Check private pickup warehouse eligibility before the SQL transaction; the client never chooses a warehouse.
        var pickupIds = methods.Where(m => m.PickupWarehouseId.HasValue).Select(m => m.PickupWarehouseId!.Value).Distinct().ToArray();
        var activePickups = pickupIds.Length == 0 ? [] : await dependencies.Warehouses(tenant, pickupIds, ct);
        var available = methods.Where(m => m.PickupWarehouseId is not { } pickup || activePickups.Any(w => w.Id == pickup)).ToArray();
        return await repository.Transaction(async token =>
        {
            var prior = await Replay<ShippingOption[]>(request.Id, "shipping.quotes.created", hash, token); if (prior is not null) return prior;
            var options = new List<ShippingOption>();
            foreach (var method in available)
            {
                var net = ShippingRules.Round(method.FreeAbove is { } free && amount >= free ? 0 : method.FlatFee + method.PerUnitFee * units, preparation.Quote.DecimalPlaces);
                var tax = ShippingRules.Round(net * method.TaxRate / 100m, preparation.Quote.DecimalPlaces); ShippingRules.Money(net); ShippingRules.Money(tax); ShippingRules.Money(net + tax);
                var expiry = preparation.ExpiresAt < Now.AddMinutes(5) ? preparation.ExpiresAt : Now.AddMinutes(5);
                var id = ShippingRules.Operation(request.Id, "method:" + method.Id.ToString("N"));
                var contract = new ShippingQuoteContract(id, tenant, method.Id, method.Name, method.Currency, country, postal, preparation.Items, net, tax, net + tax, expiry, method.PickupWarehouseId, preparation.Id, user);
                repository.Add(new ShippingQuote { TenantId = tenant, Id = id, MethodId = method.Id, PreparationId = preparation.Id, UserId = user, Document = ContractJson.Write(contract),
                    ConfigurationSnapshot = ContractJson.Write(new { Method = View(method), Zone = View(zones[method.ZoneId]) }), CreatedAt = Now, ExpiresAt = expiry });
                options.Add(new(id, method.Id, method.Name, method.Description, method.Kind, method.Currency, country, postal, net, tax, net + tax, expiry, preparation.Id));
            }
            return await Record(tenant, request.Id, "shipping.quotes.created", hash, request.Id, () => options.ToArray(), token);
        }, ct);
    }
    public async Task<ShippingQuoteContract> Quote(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Machine(tenant, false, ct); var row = await repository.Quote(id, ct) ?? throw new BusinessFault("ShippingQuoteNotFound", 404);
        if (row.ExpiresAt <= Now) throw new BusinessFault("ShippingQuoteExpired");
        var method = (await repository.Methods(ct)).SingleOrDefault(m => m.Id == row.MethodId && m.Active) ?? throw new BusinessFault("ShippingMethodInactive");
        if (!(await repository.Zones(ct)).Any(z => z.Id == method.ZoneId && z.Active)) throw new BusinessFault("ShippingZoneInactive");
        return ContractJson.Read<ShippingQuoteContract>(row.Document);
    }

    private void Tracking(Shipment shipment, string note)
    {
        shipment.Revision++; shipment.UpdatedAt = Now;
        repository.Add(new TrackingEvent { TenantId = shipment.TenantId, ShipmentId = shipment.Id, Sequence = shipment.Revision, Status = shipment.Status, Note = note, OccurredAt = Now });
    }
    public async Task<ShipmentView> CreateShipment(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Machine(tenant, true, ct); ShippingRules.Id(id);
        if (await repository.Shipment(id, ct) is { } existing) return View(existing);
        var proof = await dependencies.Fulfillment(tenant, id, ct);
        if (proof.Fulfillment.Id != id || proof.Fulfillment.OrderId != proof.Order.Id || proof.Order.Historical || proof.ShippingQuoteId is not { } quoteId
            || proof.Fulfillment.Status is not (0 or 1) || proof.Order.Status is not (1 or 2 or 3) || proof.Order.PaymentState is not (2 or 3 or 4)
            || proof.Fulfillment.Items.Length is < 1 or > 50 || proof.Fulfillment.Items.Select(i => i.VariantId).Distinct().Count() != proof.Fulfillment.Items.Length) throw new BusinessFault("OrderNotShippable");
        foreach (var item in proof.Fulfillment.Items)
        {
            ShippingRules.Id(item.VariantId); ShippingRules.Quantity(item.Quantity);
            if (!proof.Order.Items.Any(i => i.VariantId == item.VariantId && i.Quantity >= item.Quantity)) throw new BusinessFault("ShipmentQuantityMismatch");
        }
        await dependencies.Warehouse(tenant, proof.Fulfillment.WarehouseId, ct);
        var row = await repository.Quote(quoteId, ct) ?? throw new BusinessFault("OrderShippingQuoteMissing");
        var quote = ContractJson.Read<ShippingQuoteContract>(row.Document);
        if (quote.CountryCode != proof.Order.Contact.CountryCode || quote.PostalCode != proof.Order.Contact.PostalCode || quote.PickupWarehouseId is { } pickup && pickup != proof.Fulfillment.WarehouseId) throw new BusinessFault("ShipmentDestinationMismatch");
        var config = ContractJson.Read<System.Text.Json.JsonElement>(row.ConfigurationSnapshot);
        var kind = (DeliveryKind)config.GetProperty("method").GetProperty("kind").GetInt32();
        var document = ContractJson.Write(proof.Order.Contact); if (document.Length > 4000) throw new BusinessFault("ShippingContactTooLarge", 400);
        var hash = Hash(new { id, proof.Fulfillment.OrderId, proof.Fulfillment.WarehouseId, quoteId, proof.Fulfillment.Items, proof.Order.Contact });
        return await repository.Transaction(async token =>
        {
            if (await repository.Shipment(id, token) is { } replay) { if (replay.ProofHash != hash) throw new BusinessFault("ShipmentKeyReused"); return View(replay); }
            var shipment = new Shipment { TenantId = tenant, Id = id, OrderId = proof.Order.Id, QuoteId = quoteId, WarehouseId = proof.Fulfillment.WarehouseId,
                UserId = quote.UserId, Kind = kind, Provider = kind == DeliveryKind.SandboxCourier ? courier.Name : "Local", CourierReference = kind == DeliveryKind.SandboxCourier ? courier.Reference(id) : "local:" + id.ToString("N"),
                ContactDocument = document, ProofHash = hash, CreatedAt = Now, UpdatedAt = Now };
            foreach (var item in proof.Fulfillment.Items) shipment.Items.Add(new() { TenantId = tenant, ShipmentId = id, VariantId = item.VariantId, Quantity = item.Quantity });
            repository.Add(shipment); Tracking(shipment, "Shipment created from verified Order fulfillment");
            await repository.Save("shipping.shipment.created", id, new { ShipmentId = id, shipment.OrderId }, token); return View(shipment);
        }, ct);
    }
    public async Task<object> Shipments(Guid tenant, int page, ShipmentStatus? status, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Staff, ct);
        if (page is < 1 or > 10000 || status is { } filter && !Enum.IsDefined(filter)) throw new BusinessFault("InvalidShipmentQuery", 400);
        var result = await repository.Shipments(page, status, ct); return new { Items = result.Items.Select(View), result.Total, Page = page, PageSize = 50 };
    }
    public async Task<object> History(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Staff, ct); var shipment = await repository.Shipment(id, ct) ?? throw new BusinessFault("ShipmentNotFound", 404);
        return new { Shipment = View(shipment), Contact = ContractJson.Read<ShippingContact>(shipment.ContactDocument), Events = await repository.Tracking(id, ct), Callbacks=(await repository.Callbacks(id,ct)).Select(c=>new{c.Id,c.TargetStatus,c.Sent,c.Attempts,c.ManualReview,c.LastFault,c.NextAttemptAt}) };
    }
    public async Task<ShipmentView> Change(Guid tenant, Guid id, ShipmentStateRequest request, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); if (!Enum.IsDefined(request.Status)) throw new BusinessFault("InvalidShipmentStatus", 400);
        var note = ShippingRules.Text(request.Note, 300); var hash = Hash(new { id, request });
        return await repository.Transaction(async token =>
        {
            var replay = await Replay<ShipmentView>(request.OperationId, "shipping.shipment.changed", hash, token); if (replay is not null) return replay;
            var shipment = await repository.Shipment(id, token) ?? throw new BusinessFault("ShipmentNotFound", 404); repository.Version(shipment, request.Version);
            ShippingRules.Move(shipment, request.Status); Tracking(shipment, note); int? target = null;
            if (request.Status == ShipmentStatus.Dispatched && shipment.DispatchedAt is null) { shipment.DispatchedAt = Now; target = 2; }
            if (request.Status == ShipmentStatus.Delivered) { shipment.DeliveredAt = Now; target = 3; }
            if (request.Status == ShipmentStatus.Cancelled) target = 4;
            if (target is { } state) repository.Add(new ShipmentCallback { TenantId = tenant, Id = ShippingRules.Operation(id, "order:" + state), ShipmentId = id, Sequence = shipment.Revision, TargetStatus = state, NextAttemptAt = Now, CreatedAt = Now });
            return await Record(tenant, request.OperationId, "shipping.shipment.changed", hash, id, () => View(shipment), token, new { ShipmentId = id, shipment.OrderId, shipment.Status });
        }, ct);
    }
    public async Task<object> CustomerTracking(Guid tenant, Guid order, bool guest, string? orderKey, CancellationToken ct)
    {
        if (guest) await access.Public(tenant, false, ct); else await access.Human(tenant, ActorPermission.Customer, ct);
        await dependencies.VerifyCustomerOrder(tenant, order, guest, orderKey, ct);
        var result = new List<object>(); foreach (var shipment in await repository.OrderShipments(order, ct))
            result.Add(new { Shipment = View(shipment), Events = (await repository.Tracking(shipment.Id, ct)).Select(e => new { e.Status, e.Note, e.OccurredAt }) });
        return result;
    }
    public async Task<ReturnPickupView> Pickup(Guid tenant, ReturnPickupRequest request, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); ShippingRules.Id(request.Id); ShippingRules.Id(request.ReturnId); ShippingRules.Id(request.WarehouseId);
        var hash = Hash(request);var previous = await Replay<ReturnPickupView>(request.Id,"shipping.pickup.scheduled",hash,ct);if(previous is not null)return previous;
        if (request.WindowFrom < Now || request.WindowFrom > Now.AddDays(30) || request.WindowTo <= request.WindowFrom || request.WindowTo > request.WindowFrom.AddHours(12)) throw new BusinessFault("InvalidPickupWindow", 400);
        var proof = await dependencies.Return(tenant, request.ReturnId, ct);
        if (proof.Return.Id != request.ReturnId || proof.Return.OrderId != proof.Order.Id || proof.Order.Historical || proof.Return.Status != 1) throw new BusinessFault("ApprovedReturnRequired");
        await dependencies.Warehouse(tenant, request.WarehouseId, ct);
        return await repository.Transaction(async token =>
        {
            var replay = await Replay<ReturnPickupView>(request.Id, "shipping.pickup.scheduled", hash, token); if (replay is not null) return replay;
            if(await repository.ActivePickup(request.ReturnId,token) is not null)throw new BusinessFault("ReturnPickupAlreadyScheduled");
            var pickup = new ReturnPickup { TenantId = tenant, Id = request.Id, ReturnId=request.ReturnId, OrderId = proof.Order.Id, WarehouseId = request.WarehouseId,
                ContactDocument = ContractJson.Write(proof.Order.Contact), Items = ContractJson.Write(proof.Return.Items), WindowFrom = request.WindowFrom, WindowTo = request.WindowTo, CreatedAt = Now };
            repository.Add(pickup); return await Record(tenant, request.Id, "shipping.pickup.scheduled", hash, request.Id, () => View(pickup), token, new { PickupId=request.Id,ReturnId = request.ReturnId, OrderId=proof.Order.Id });
        }, ct);
    }
    public async Task<ReturnPickupView> ChangePickup(Guid tenant, Guid id, ReturnPickupStateRequest request, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); var note = ShippingRules.Text(request.Note, 300); var hash = Hash(new { id, request });
        return await repository.Transaction(async token =>
        {
            var replay = await Replay<ReturnPickupView>(request.OperationId, "shipping.pickup.changed", hash, token); if (replay is not null) return replay;
            var pickup = await repository.Pickup(id, token) ?? throw new BusinessFault("ReturnPickupNotFound", 404); repository.Version(pickup, request.Version);
            if (pickup.Status != PickupStatus.Scheduled || request.Status is not (PickupStatus.Collected or PickupStatus.Cancelled)) throw new BusinessFault("InvalidPickupTransition");
            pickup.Status = request.Status; pickup.Note = note;
            return await Record(tenant, request.OperationId, "shipping.pickup.changed", hash, id, () => View(pickup), token, new { ReturnId = id, pickup.OrderId, pickup.Status });
        }, ct);
    }
    public async Task<ReturnPickupView[]> Pickups(Guid tenant, CancellationToken ct) { await access.Human(tenant, ActorPermission.Staff, ct); return (await repository.Pickups(ct)).Select(View).ToArray(); }
    public async Task<object> Recovery(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct);
        return await repository.Transaction(async token =>
        {
            var callback = await repository.FreshCallback(id, token) ?? throw new BusinessFault("ShipmentCallbackNotFound", 404);
            if (!callback.Sent) { callback.ManualReview = false; callback.Attempts = 0; callback.NextAttemptAt = Now; callback.LeaseId = null; callback.LeaseUntil = null; await repository.Save("shipping.callback.retry", id, null, token); }
            return new { callback.Id, callback.Sent, callback.Attempts, callback.ManualReview };
        }, ct);
    }
}
