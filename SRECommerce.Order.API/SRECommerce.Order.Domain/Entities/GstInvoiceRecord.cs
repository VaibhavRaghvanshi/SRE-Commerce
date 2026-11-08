using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Order.Domain;

public sealed class GstInvoiceRecord : ITenantRecord
{
    public Guid TenantId { get; set; }
    public Guid Id { get; set; }
    public string SupplierGstin { get; set; } = "";
    public int FinancialYearStart { get; set; }
    public string Number { get; set; } = "";
    public string? Irn { get; set; }
    public string RequestHash { get; set; } = "";
    public string Document { get; set; } = "";
}
