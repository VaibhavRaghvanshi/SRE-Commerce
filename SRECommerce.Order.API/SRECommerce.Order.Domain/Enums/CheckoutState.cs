namespace SRECommerce.Order.Domain;
public enum CheckoutState { AwaitingProof, Processing, Accepted, Compensating, Failed, ManualReview, Cancelled }
