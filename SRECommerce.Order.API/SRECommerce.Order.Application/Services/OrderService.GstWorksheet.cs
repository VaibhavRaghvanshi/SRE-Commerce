using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Order.Domain;

namespace SRECommerce.Order.Application;

public sealed partial class OrderService
{
    public async Task<GstWorksheet> PreviewGstWorksheet(Guid tenant, Guid id, GstWorksheetRequest request, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ActorPermission.Manager, ct);
        var order = await Owned(id, false, null, true, ct);
        return CreateGstWorksheet(order, request);
    }

    internal static GstWorksheet CreateGstWorksheet(SalesOrder order, GstWorksheetRequest request)
    {
        if (!string.Equals(request.Version, Version(order.Version), StringComparison.Ordinal))
            throw new BusinessFault("OrderVersionConflict");
        if (order.Historical || order.Currency != "INR" || order.DecimalPlaces != 2 || order.Refunded != 0
            || order.Status is not (OrderStatus.Confirmed or OrderStatus.Processing or OrderStatus.PartiallyFulfilled
                or OrderStatus.Fulfilled or OrderStatus.Delivered or OrderStatus.Completed))
            throw new BusinessFault("GstWorksheetOrderUnavailable");

        var contact = ContractJson.Read<OrderContact>(order.ContactDocument);
        if (contact.CountryCode != "IN") throw new BusinessFault("GstWorksheetDomesticOnly");
        if (request.Supplier is null || request.Recipient is null)
            throw new BusinessFault("GstPartiesRequired", 400);
        var supplier = NormalizeGstParty(request.Supplier, true);
        var recipient = NormalizeGstParty(request.Recipient, false);
        var placeCode = OrderRules.Text(request.PlaceOfSupplyCode, 2);
        if (!Regex.IsMatch(placeCode, "\\A[0-9]{2}\\z") || placeCode == "00")
            throw new BusinessFault("InvalidPlaceOfSupply", 400);
        var placeName = OrderRules.Text(request.PlaceOfSupplyName, 80);
        var treatment = request.TaxTreatment;
        if (treatment is not null)
        {
            var interstate = !supplier.Gstin!.StartsWith(placeCode, StringComparison.Ordinal);
            if (request.ReverseCharge || treatment is not ("InterState" or "IntraState" or "IntraTerritory") ||
                interstate != (treatment == "InterState"))
                throw new BusinessFault("GstTaxTreatmentMismatch", 400);
        }

        if (request.Items is null || request.Items.Length != order.Items.Count || request.Items.Any(item => item is null)
            || request.Items.Select(item => item.VariantId).Distinct().Count() != request.Items.Length)
            throw new BusinessFault("GstClassificationsRequired", 400);
        var classifications = request.Items.ToDictionary(item => item.VariantId);
        var quote = ContractJson.Read<SnapshotQuote>(order.PriceSnapshot);
        var recordedLines = quote.Calculation.Lines.ToDictionary(line => line.VariantId);
        var lines = new List<GstWorksheetLine>(order.Items.Count);
        foreach (var item in order.Items.OrderBy(item => item.Sku))
        {
            if (!classifications.TryGetValue(item.VariantId, out var classification))
                throw new BusinessFault("GstClassificationsRequired", 400);
            var hsn = OrderRules.Text(classification.Hsn, 8);
            var unit = OrderRules.Text(classification.UnitCode, 3).ToUpperInvariant();
            if (!Regex.IsMatch(hsn, "\\A(?:[0-9]{4}|[0-9]{6}|[0-9]{8})\\z") || !Regex.IsMatch(unit, "\\A[A-Z]{3}\\z"))
                throw new BusinessFault("InvalidGstItemClassification", 400);
            if (!recordedLines.TryGetValue(item.VariantId, out var recorded) || recorded.Net != item.Net
                || recorded.Quantity != item.Quantity || recorded.Tax != item.Tax || recorded.Total != item.Total)
                throw new BusinessFault("GstWorksheetSnapshotMismatch");
            lines.Add(new(item.Sku, item.Name, hsn, unit, item.Quantity, item.Discount, item.Net, recorded.TaxRate, item.Tax, item.Total,
                DraftComponents(treatment, item.Tax)));
        }

        var shipping = ContractJson.Read<ShippingQuoteSnapshot>(order.ShippingSnapshot);
        var shippingSac = request.ShippingSac?.Trim();
        if (shipping.Total > 0 && (shippingSac is null || !Regex.IsMatch(shippingSac, "\\A[0-9]{6}\\z")))
            throw new BusinessFault("GstShippingSacRequired", 400);
        if (shipping.Total == 0) shippingSac = null;
        var itemTax = lines.Sum(line => line.RecordedTax);
        if (shipping.Currency != order.Currency || shipping.Total != order.ShippingTotal || shipping.Net + shipping.Tax != shipping.Total
            || itemTax != order.Tax || lines.Sum(line => line.Total) + shipping.Total != order.Total)
            throw new BusinessFault("GstWorksheetSnapshotMismatch");

        var deliveryAddress = string.Join(", ", new[] { contact.AddressLine1, contact.AddressLine2, contact.City, contact.PostalCode, contact.CountryCode }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
        var shippingComponents = DraftComponents(treatment, shipping.Tax);
        var totalComponents = shippingComponents is null ? null : new GstTaxAmounts(
            lines.Sum(line => line.TaxComponents!.Cgst) + shippingComponents.Cgst,
            lines.Sum(line => line.TaxComponents!.Sgst) + shippingComponents.Sgst,
            lines.Sum(line => line.TaxComponents!.Utgst) + shippingComponents.Utgst,
            lines.Sum(line => line.TaxComponents!.Igst) + shippingComponents.Igst);
        return new("GST_WORKSHEET_DRAFT", order.Number, order.CreatedAt, order.InvoiceReference, order.Currency, supplier, recipient,
            deliveryAddress, placeCode, placeName, request.ReverseCharge, lines.ToArray(),
            new(shipping.MethodName, shippingSac, shipping.Net, shipping.Tax, shipping.Total, shippingComponents),
            itemTax + shipping.Tax, order.Total, treatment, totalComponents);
    }

    private static GstTaxAmounts? DraftComponents(string? treatment, decimal tax)
    {
        if (treatment is null) return null;
        if (tax != decimal.Round(tax, 2)) throw new BusinessFault("GstTaxPrecisionMismatch");
        // Allocate an odd paisa to the local component so the saved line still reconciles.
        var central = decimal.Floor(tax * 50) / 100;
        var local = tax - central;
        return treatment switch
        {
            "IntraState" => new(central, local, 0, 0),
            "IntraTerritory" => new(central, 0, local, 0),
            _ => new(0, 0, 0, tax)
        };
    }

    private static GstParty NormalizeGstParty(GstParty party, bool required)
    {
        var gstin = party.Gstin?.Trim().ToUpperInvariant();
        if ((required || !string.IsNullOrEmpty(gstin)) && (gstin is null || !Regex.IsMatch(gstin, "\\A[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][0-9A-Z]Z[0-9A-Z]\\z")))
            throw new BusinessFault("InvalidGstinFormat", 400);
        return new(OrderRules.Text(party.Name), OrderRules.Text(party.Address, 600), string.IsNullOrEmpty(gstin) ? null : gstin);
    }
}
