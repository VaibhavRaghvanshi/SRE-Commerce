namespace SRECommerce.Payment.Application;
public interface IOrderPaymentProofs { Task<OrderPaymentProof> Get(Guid tenant, Guid order, CancellationToken ct); }
