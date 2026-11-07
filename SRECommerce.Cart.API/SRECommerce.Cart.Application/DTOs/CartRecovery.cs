namespace SRECommerce.Cart.Application;

public sealed record CartRecoveryCandidate(Guid Id, DateTimeOffset ExpiredAt, int ItemCount);
public sealed record RecoverCartRequest(Guid OperationId, Guid SourceCartId, string Version);
