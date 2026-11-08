using System.Text.Json.Serialization;

namespace SRECommerce.Order.Application;

public sealed record RecordGstInvoiceRequest(GstWorksheetRequest Worksheet, string Number, DateOnly IssuedOn,
    string EvidenceReference, [property: JsonRequired] bool RequiresIrn, string? Irn,
    GstTaxAmounts TaxComponents, decimal Total, [property: JsonRequired] bool ExternallyIssued);

public sealed record GstInvoice(string DocumentType, Guid OrderId, string Number, DateOnly IssuedOn,
    int FinancialYearStart, string EvidenceReference, bool RequiresIrn, string? Irn,
    GstTaxAmounts IssuedTaxComponents, GstWorksheet Worksheet, DateTimeOffset RecordedAt);
