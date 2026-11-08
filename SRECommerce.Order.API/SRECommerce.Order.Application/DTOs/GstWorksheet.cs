using System.Text.Json.Serialization;

namespace SRECommerce.Order.Application;

public sealed record GstParty(string Name, string Address, string? Gstin);
public sealed record GstClassification(Guid VariantId, string Hsn, string UnitCode);
public sealed record GstWorksheetRequest(string Version, GstParty Supplier, GstParty Recipient, string PlaceOfSupplyCode,
    string PlaceOfSupplyName, bool ReverseCharge, GstClassification[] Items, string? ShippingSac, string? TaxTreatment = null);
public sealed record GstTaxAmounts(decimal Cgst, decimal Sgst, decimal Utgst, decimal Igst);
public sealed record GstWorksheetLine(string Sku, string Description, string Classification, string UnitCode, decimal Quantity,
    decimal Discount, decimal TaxableValue, decimal RecordedTaxRate, decimal RecordedTax, decimal Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GstTaxAmounts? TaxComponents = null);
public sealed record GstWorksheetShipping(string Method, string? Sac, decimal TaxableValue, decimal RecordedTax, decimal Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GstTaxAmounts? TaxComponents = null);
public sealed record GstWorksheet(string DocumentType, string OrderNumber, DateTimeOffset OrderDate, string? ExternalInvoiceReference,
    string Currency, GstParty Supplier, GstParty Recipient, string DeliveryAddress, string PlaceOfSupplyCode, string PlaceOfSupplyName,
    bool ReverseCharge, GstWorksheetLine[] Lines, GstWorksheetShipping Shipping, decimal RecordedTax, decimal Total,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TaxTreatment = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] GstTaxAmounts? TaxComponents = null);
