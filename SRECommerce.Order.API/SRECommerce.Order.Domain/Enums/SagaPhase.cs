namespace SRECommerce.Order.Domain;
public enum SagaPhase { AwaitCartClaim, Validate, ClaimPrice, PlanStock, ReserveStock, StartPayment, WaitPayment, CapturePayment, CommitStock, RedeemPrice, CompleteCart, Confirm, CompensatePayment, CompensateStock, ReleasePrice, ReleaseCart, Done }
