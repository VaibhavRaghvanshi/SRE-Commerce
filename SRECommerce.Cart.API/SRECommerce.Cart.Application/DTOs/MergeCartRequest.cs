namespace SRECommerce.Cart.Application;
public sealed record MergeCartRequest(Guid OperationId,Guid GuestCartId,string GuestVersion,string Version);
