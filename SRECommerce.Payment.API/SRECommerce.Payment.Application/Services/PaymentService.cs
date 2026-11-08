using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Payment.Domain;

namespace SRECommerce.Payment.Application;

public sealed class PaymentService(IPaymentRepository repository, IPaymentAccess access, IOrderPaymentProofs orders,
    IPaymentProvider provider, IPaymentSignatures signatures, TimeProvider clock) : IPaymentService
{
    private DateTimeOffset Now => clock.GetUtcNow();
    private static string Version(byte[] value) => Convert.ToBase64String(value);
    private static PaymentView View(PaymentTransaction p) => new(p.Id, p.OrderId, p.Currency, p.Amount, p.Method,
        p.Status, p.Refunded, Version(p.Version), p.Provider, p.ProviderReference, p.Provider == "Sandbox", p.DecimalPlaces);
    private static PaymentConfigView View(PaymentProviderConfiguration c) => new("Sandbox", c.Active, c.AllowCard,
        c.AllowUPI, c.AllowNetBanking, c.AllowCOD, c.Outcome, Version(c.Version));
    private Task<PaymentTransaction> Required(Guid order, CancellationToken ct) => Find(order, ct);
    private async Task<PaymentTransaction> Find(Guid order, CancellationToken ct) =>
        await repository.Payment(order, ct) ?? throw new BusinessFault("PaymentNotFound", 404);
    private void Attempt(PaymentTransaction payment, string action, PaymentStatus? previous)
    {
        payment.Revision++;
        payment.UpdatedAt = Now;
        repository.Add(new PaymentAttempt { TenantId = payment.TenantId, PaymentId = payment.Id,
            Sequence = payment.Revision, Action = action, Previous = previous, Current = payment.Status,
            ProviderReference = payment.ProviderReference, CreatedAt = Now });
    }
    private static void Proof(Guid tenant, Guid order, OrderPaymentProof proof)
    {
        if (proof.TenantId != tenant || proof.Id != order || proof.Historical) throw new BusinessFault("OrderPaymentProofRejected");
        PaymentRules.OrderAmount(proof.Total); PaymentRules.Currency(proof.Currency);
        if (proof.DecimalPlaces is < 0 or > 3 || decimal.Round(proof.Total, proof.DecimalPlaces) != proof.Total) throw new BusinessFault("OrderPaymentPrecisionRejected");
        if (proof.PaymentMethod is not ("Card" or "UPI" or "NetBanking" or "COD" or "GiftCard" or "CounterCash"))
            throw new BusinessFault("UnsupportedPaymentMethod", 400);
        if (proof.PaymentMethod == "GiftCard" && (proof.GiftCardId is null || proof.GiftCardId == Guid.Empty || proof.UserId is null))
            throw new BusinessFault("GiftCardPaymentProofRejected");
        if (proof.PaymentMethod == "CounterCash" && proof.UserId is null) throw new BusinessFault("CounterPaymentProofRejected");
    }
    private PaymentTransaction New(OrderPaymentProof proof) => new() { TenantId = proof.TenantId, Id = proof.Id,
        OrderId = proof.Id, UserId = proof.UserId, Currency = proof.Currency, DecimalPlaces = proof.DecimalPlaces, Amount = proof.Total,
        Method = proof.PaymentMethod, Provider = proof.PaymentMethod switch { "GiftCard" => "GiftCard", "CounterCash" => "Counter", _ => provider.Name },
        OrderProof = ContractJson.Write(proof), CreatedAt = Now, UpdatedAt = Now };

    private static string LocalReference(string provider, Guid operation) => provider + "-" + operation.ToString("N");

    private async Task<GiftCard> DebitGiftCard(OrderPaymentProof proof, CancellationToken ct)
    {
        var card = await repository.GiftCard(proof.GiftCardId!.Value, ct);
        if (card is null || card.ClaimedBy != proof.UserId || card.VoidedAt is not null || card.Currency != proof.Currency)
            throw new BusinessFault("GiftCardUnavailable", 404);
        if (card.Balance < proof.Total) throw new BusinessFault("GiftCardInsufficientBalance");
        card.Balance -= proof.Total;
        return card;
    }

    public async Task<PaymentView> Start(Guid tenant, Guid order, CancellationToken ct)
    {
        PaymentRules.Id(order); await access.Machine(tenant, false, ct);
        if (await repository.Payment(order, ct) is { } existing) return View(existing);
        // Fetch authoritative facts outside the local SQL transaction. No browser amount or card data is accepted.
        var proof = await orders.Get(tenant, order, ct); Proof(tenant, order, proof);
        if (proof.Status != 0) throw new BusinessFault("OrderNotAwaitingPayment");
        return await repository.Transaction(async token =>
        {
            if (await repository.Payment(order, token) is { } replay) return View(replay);
            if (proof.PaymentMethod is "GiftCard" or "CounterCash")
            {
                var local = New(proof);
                if (proof.PaymentMethod == "GiftCard") local.GiftCardId = (await DebitGiftCard(proof, token)).Id;
                local.Status = PaymentStatus.Captured;
                local.ProviderReference = LocalReference(local.Provider, local.Id);
                repository.Add(local);
                Attempt(local, local.Provider == "GiftCard" ? "giftcard.debited" : "counter.cash.recorded", null);
                await repository.Save("payment.captured", local.Id, local, token);
                return View(local);
            }
            var config = await repository.Configuration(token) ?? throw new BusinessFault("PaymentsNotConfigured");
            PaymentRules.Enabled(config, proof.PaymentMethod);
            var payment = New(proof); var receipt = provider.Begin(payment.Id, payment.Method, config.Outcome);
            payment.Status = payment.Amount == 0 ? PaymentStatus.Captured : receipt.Status; payment.ProviderReference = receipt.Reference; repository.Add(payment);
            Attempt(payment, payment.Amount == 0 ? "zero-amount.completed" : "initiated", null); await repository.Save("payment.initiated", payment.Id, payment, token);
            return View(payment);
        }, ct);
    }

    public async Task<PaymentView> Read(Guid tenant, Guid order, bool machine, bool customer, CancellationToken ct)
    {
        if (machine) await access.Machine(tenant, true, ct);
        else await access.Human(tenant, customer ? ActorPermission.Customer : ActorPermission.Staff, ct);
        var payment = await Required(order, ct);
        if (customer && payment.UserId != access.Actor?.UserId) throw new BusinessFault("PaymentNotFound", 404);
        return View(payment);
    }

    public async Task<PaymentView> Capture(Guid tenant, Guid order, string version, CancellationToken ct)
    {
        await access.Machine(tenant, false, ct);
        var proof = await orders.Get(tenant, order, ct); Proof(tenant, order, proof);
        return await repository.Transaction(async token =>
        {
            var payment = await Required(order, token);
            if (payment.Amount != proof.Total || payment.Currency != proof.Currency || payment.DecimalPlaces != proof.DecimalPlaces || payment.Method != proof.PaymentMethod) throw new BusinessFault("OrderPaymentFactsChanged");
            if (payment.Status is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded) return View(payment);
            if (payment.Provider != provider.Name || payment.CancellationRequested || payment.Status is not (PaymentStatus.Authorized or PaymentStatus.CashOnDelivery)) throw new BusinessFault("PaymentNotCapturable");
            if (payment.Method == "COD" ? proof.Status is not (5 or 6) : proof.Status != 0) throw new BusinessFault("CaptureRequiresAuthoritativeOrderState");
            repository.Version(payment, version); var previous = payment.Status;
            var receipt = provider.Capture(payment.Id); payment.Status = receipt.Status; payment.ProviderReference = receipt.Reference;
            Attempt(payment, payment.Method == "COD" ? "sandbox.cod.settled" : "captured", previous);
            await repository.Save("payment.captured", payment.Id, payment, token); return View(payment);
        }, ct);
    }

    public async Task<PaymentView> Abort(Guid tenant, Guid order, CancellationToken ct)
    {
        await access.Machine(tenant, true, ct); PaymentRules.Id(order);
        var found = await repository.Payment(order, ct);
        OrderPaymentProof? proof = null;
        if (found is null) { proof = await orders.Get(tenant, order, ct); Proof(tenant, order, proof); }
        return await repository.Transaction(async token =>
        {
            var payment = await repository.Payment(order, token);
            if (payment?.CancellationRequested == true) return View(payment);
            PaymentStatus? previous = payment?.Status;
            if (payment is null)
            {
                payment = New(proof!);
                payment.ProviderReference = payment.Provider == provider.Name
                    ? provider.Begin(payment.Id, payment.Method, SandboxOutcome.Pending).Reference : LocalReference(payment.Provider, payment.Id);
                repository.Add(payment);
            }
            payment.CancellationRequested = true;
            if (payment.Status is PaymentStatus.Pending or PaymentStatus.Authorized or PaymentStatus.CashOnDelivery) payment.Status = PaymentStatus.Cancelled;
            Attempt(payment, "cancellation.requested", previous);
            await repository.Save("payment.cancelled", payment.Id, payment, token); return View(payment);
        }, ct);
    }

    public async Task<PaymentView> Refund(Guid tenant, Guid order, RefundRequest request, CancellationToken ct)
    {
        await access.Machine(tenant, true, ct); PaymentRules.Id(request.Id);
        var reason = PaymentRules.Reason(request.Reason);
        return await repository.Transaction(async token =>
        {
            var payment = await Required(order, token); var prior = await repository.Refund(request.Id, token);
            if (prior is not null)
            {
                if (prior.PaymentId != payment.Id || prior.Amount != request.Amount || prior.Reason != reason) throw new BusinessFault("RefundKeyReused");
                return View(payment);
            }
            PaymentRules.Refund(payment, request.Amount); var previous = payment.Status;
            var reference = payment.Provider == provider.Name ? provider.Refund(request.Id) : LocalReference(payment.Provider, request.Id);
            if (payment.Provider == "GiftCard")
            {
                var card = await repository.GiftCard(payment.GiftCardId!.Value, token) ?? throw new BusinessFault("GiftCardLedgerMissing", 500);
                card.Balance += request.Amount;
            }
            repository.Add(new RefundTransaction { TenantId = tenant, Id = request.Id, PaymentId = payment.Id,
                Amount = request.Amount, Reason = reason, ProviderReference = reference, CreatedAt = Now });
            payment.Refunded += request.Amount;
            payment.Status = payment.Refunded == payment.Amount ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
            var action = payment.Provider switch { "GiftCard" => "giftcard.credited", "Counter" => "counter.cash.refund.recorded", _ => "refunded" };
            Attempt(payment, action, previous); await repository.Save("payment.refunded", payment.Id, payment, token);
            return View(payment);
        }, ct);
    }

    public async Task<PaymentConfigView> Configuration(Guid tenant, PaymentConfigRequest? request, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Owner, ct);
        if (request is null) return View(await repository.Configuration(ct) ?? throw new BusinessFault("PaymentsNotConfigured", 404));
        if (!Enum.IsDefined(request.Outcome)) throw new BusinessFault("InvalidSandboxOutcome", 400);
        return await repository.Transaction(async token =>
        {
            var config = await repository.Configuration(token);
            if (config is null) { config = new() { TenantId = tenant, Id = tenant }; repository.Add(config); }
            else repository.Version(config, request.Version ?? "");
            config.Active = request.Active; config.AllowCard = request.AllowCard; config.AllowUPI = request.AllowUPI;
            config.AllowNetBanking = request.AllowNetBanking; config.AllowCOD = request.AllowCOD; config.Outcome = request.Outcome; config.UpdatedAt = Now;
            await repository.Save("payment.configuration.saved", tenant, null, token); return View(config);
        }, ct);
    }

    public async Task<object> Methods(Guid tenant, CancellationToken ct)
    {
        await access.Public(tenant, ct); var config = await repository.Configuration(ct);
        string[] methods = config is null || !config.Active ? [] : new[] { ("Card", config.AllowCard), ("UPI", config.AllowUPI), ("NetBanking", config.AllowNetBanking), ("COD", config.AllowCOD) }.Where(m => m.Item2).Select(m => m.Item1).ToArray();
        return new { Provider = "Sandbox", Sandbox = true, Configured = config is not null && config.Active, Methods = methods };
    }

    public async Task<object> List(Guid tenant, int page, PaymentStatus? status, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Staff, ct);
        if (page is < 1 or > 10000 || status is { } state && !Enum.IsDefined(state)) throw new BusinessFault("InvalidPaymentQuery", 400);
        var result = await repository.List(page, status, ct);
        return new { Items = result.Items.Select(View), result.Total, Page = page, PageSize = 50 };
    }

    public async Task<object> History(Guid tenant, Guid order, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Staff, ct); var payment = await Required(order, ct);
        return new { Attempts = await repository.Attempts(payment.Id, ct), Refunds = await repository.Refunds(payment.Id, ct) };
    }

    public async Task<object> Reconcile(Guid tenant, Guid order, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); var payment = await Required(order, ct);
        var refunds = await repository.Refunds(payment.Id, ct); var attempts = await repository.Attempts(payment.Id, ct);
        var sandbox = payment.Provider == provider.Name;
        var referenceMatches = sandbox ? provider.VerifyReference(payment.Id, payment.ProviderReference)
            : payment.ProviderReference == LocalReference(payment.Provider, payment.Id);
        var consistent = referenceMatches && refunds.Sum(r => r.Amount) == payment.Refunded
            && attempts.LastOrDefault()?.Current == payment.Status && payment.Refunded <= payment.Amount;
        await repository.Save("payment.reconciled", payment.Id, null, ct);
        return new { PaymentId = payment.Id, payment.Provider, Sandbox = sandbox, Consistent = consistent,
            Captured = payment.Status is PaymentStatus.Captured or PaymentStatus.PartiallyRefunded or PaymentStatus.Refunded ? payment.Amount : 0,
            payment.Refunded, RefundCount = refunds.Length, AttemptCount = attempts.Length };
    }

    public async Task<object> Webhook(Guid tenant, string document, string timestamp, string signature, CancellationToken ct)
    {
        if (Encoding.UTF8.GetByteCount(document) > 16384) throw new BusinessFault("WebhookTooLarge", 413);
        signatures.Verify(tenant, timestamp, document, signature, Now);
        SandboxWebhook webhook;
        try { webhook = ContractJson.Read<SandboxWebhook>(document); }
        catch (JsonException) { throw new BusinessFault("InvalidSandboxWebhook", 400); }
        PaymentRules.Id(webhook.EventId); PaymentRules.Id(webhook.PaymentId);
        if (webhook.Outcome is not ("Authorized" or "Declined")) throw new BusinessFault("InvalidSandboxWebhookOutcome", 400);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document)));
        access.BindWebhook(tenant);
        return await repository.Transaction(async token =>
        {
            var prior = await repository.Webhook(webhook.EventId, token);
            if (prior is not null)
            {
                if (prior.PayloadHash != digest || prior.PaymentId != webhook.PaymentId) throw new BusinessFault("WebhookKeyReused");
                return new { Accepted = true, prior.Applied };
            }
            var payment = await Required(webhook.PaymentId, token);
            if (payment.Provider != provider.Name) throw new BusinessFault("SandboxPaymentRequired", 400);
            if (payment.Amount != webhook.Amount || payment.Currency != webhook.Currency) throw new BusinessFault("WebhookAmountMismatch");
            var applied = payment.Status == PaymentStatus.Pending && !payment.CancellationRequested;
            if (applied)
            {
                var previous = payment.Status;
                payment.Status = webhook.Outcome == "Authorized" ? PaymentStatus.Authorized : PaymentStatus.Failed;
                Attempt(payment, "webhook." + webhook.Outcome.ToLowerInvariant(), previous);
            }
            repository.Add(new PaymentWebhook { TenantId = tenant, Id = webhook.EventId, PaymentId = payment.Id,
                PayloadHash = digest, Outcome = webhook.Outcome, Applied = applied,
                ProviderTimestamp = DateTimeOffset.FromUnixTimeSeconds(long.Parse(timestamp, CultureInfo.InvariantCulture)), ReceivedAt = Now });
            await repository.Save("payment.webhook.received", payment.Id, applied ? payment : null, token);
            return new { Accepted = true, Applied = applied };
        }, ct);
    }

    public async Task<object> Decide(Guid tenant, Guid order, SandboxDecisionRequest request, CancellationToken ct)
    {
        await access.Human(tenant, ActorPermission.Manager, ct); PaymentRules.Id(request.Id);
        var payment = await Required(order, ct);
        if (payment.Provider != provider.Name) throw new BusinessFault("SandboxPaymentRequired", 400);
        // A completed operation can be replayed even though its original version is now stale.
        if (await repository.Webhook(request.Id, ct) is null) repository.Version(payment, request.Version);
        var timestamp = Now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var document = ContractJson.Write(new SandboxWebhook(request.Id, payment.Id, payment.Currency, payment.Amount, request.Outcome));
        return await Webhook(tenant, document, timestamp, signatures.Sign(tenant, timestamp, document), ct);
    }
}
