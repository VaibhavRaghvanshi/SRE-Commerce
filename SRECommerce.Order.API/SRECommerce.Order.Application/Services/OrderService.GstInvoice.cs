using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Order.Domain;

namespace SRECommerce.Order.Application;

public sealed partial class OrderService
{
    private static readonly TimeSpan IndiaOffset = TimeSpan.FromMinutes(330);

    public async Task<GstInvoice> ReadGstInvoice(Guid tenant, Guid id, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ActorPermission.Manager, ct);
        return ContractJson.Read<GstInvoice>((await repository.GstInvoice(id, ct)
            ?? throw new BusinessFault("GstInvoiceNotRecorded", 404)).Document);
    }

    public async Task<GstInvoice> RecordGstInvoice(Guid tenant, Guid id, RecordGstInvoiceRequest request, CancellationToken ct)
    {
        await access.Authorize(tenant, false, ActorPermission.Manager, ct);
        if (!request.ExternallyIssued || request.Worksheet is null || request.TaxComponents is null)
            throw new BusinessFault("GstExternalInvoiceEvidenceRequired", 400);
        var number = OrderRules.Text(request.Number, 16);
        if (!Regex.IsMatch(number, "\\A[A-Za-z0-9/-]{1,16}\\z") || !number.Any(char.IsAsciiLetterOrDigit))
            throw new BusinessFault("InvalidGstInvoiceNumber", 400);
        var evidence = OrderRules.Text(request.EvidenceReference, 200);
        var irn = request.Irn?.Trim().ToUpperInvariant();
        if (irn == "") irn = null;
        if (request.RequiresIrn && irn is null || irn is not null && !Regex.IsMatch(irn, "\\A[0-9A-F]{64}\\z"))
            throw new BusinessFault("InvalidGstIrn", 400);
        var hash = Hash(request with { Number = number, EvidenceReference = evidence, Irn = irn });

        return await repository.Transaction(async token =>
        {
            if (await repository.GstInvoice(id, token) is { } existing)
            {
                if (existing.RequestHash != hash) throw new BusinessFault("GstInvoiceAlreadyRecorded");
                return ContractJson.Read<GstInvoice>(existing.Document);
            }

            var order = await Owned(id, false, null, true, token);
            var worksheet = CreateGstWorksheet(order, request.Worksheet);
            if (worksheet.TaxComponents is null || worksheet.RecordedTax <= 0)
                throw new BusinessFault("GstTaxTreatmentRequired", 400);
            if (request.Total != worksheet.Total) throw new BusinessFault("GstInvoiceTotalMismatch", 400);
            ReconcileIssuedComponents(worksheet, request.TaxComponents);

            var issueDate = request.IssuedOn;
            var recordedAt = Now;
            if (issueDate < DateOnly.FromDateTime(order.CreatedAt.ToOffset(IndiaOffset).DateTime) ||
                issueDate > DateOnly.FromDateTime(recordedAt.ToOffset(IndiaOffset).DateTime))
                throw new BusinessFault("InvalidGstInvoiceDate", 400);
            var year = issueDate.Month < 4 ? issueDate.Year - 1 : issueDate.Year;
            var invoice = new GstInvoice("GST_EXTERNAL_INVOICE_RECORD", id, number, issueDate, year, evidence,
                request.RequiresIrn, irn, request.TaxComponents, worksheet, recordedAt);
            repository.Add(new GstInvoiceRecord
            {
                TenantId = tenant, Id = id, SupplierGstin = worksheet.Supplier.Gstin!, FinancialYearStart = year,
                Number = number, Irn = irn, RequestHash = hash, Document = ContractJson.Write(invoice)
            });
            await repository.Commit("order.gst.external-invoice.recorded", id, null, token);
            return invoice;
        }, ct);
    }

    private static void ReconcileIssuedComponents(GstWorksheet worksheet, GstTaxAmounts tax)
    {
        var amounts = new[] { tax.Cgst, tax.Sgst, tax.Utgst, tax.Igst };
        if (amounts.Any(amount => amount < 0 || amount > worksheet.RecordedTax || amount != decimal.Round(amount, 2)) ||
            amounts.Sum() != worksheet.RecordedTax)
            throw new BusinessFault("GstIssuedTaxTotalMismatch", 400);

        var tolerance = 0.01m * (worksheet.Lines.Count(line => line.RecordedTax > 0) + (worksheet.Shipping.RecordedTax > 0 ? 1 : 0));
        var valid = worksheet.TaxTreatment switch
        {
            "InterState" => tax.Cgst == 0 && tax.Sgst == 0 && tax.Utgst == 0,
            "IntraState" => tax.Igst == 0 && tax.Utgst == 0 && Math.Abs(tax.Cgst - tax.Sgst) <= tolerance,
            "IntraTerritory" => tax.Igst == 0 && tax.Sgst == 0 && Math.Abs(tax.Cgst - tax.Utgst) <= tolerance,
            _ => false
        };
        if (!valid) throw new BusinessFault("GstIssuedTaxTreatmentMismatch", 400);
    }
}
