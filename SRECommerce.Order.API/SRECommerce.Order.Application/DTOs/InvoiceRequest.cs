using SRECommerce.Order.Domain;
namespace SRECommerce.Order.Application;
public sealed record InvoiceRequest(Guid OperationId,string Version,string Reference);
