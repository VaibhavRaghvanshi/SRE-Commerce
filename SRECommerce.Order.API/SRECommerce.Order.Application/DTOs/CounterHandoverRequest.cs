namespace SRECommerce.Order.Application;

public sealed record CounterHandoverRequest(Guid OperationId, string Version);
