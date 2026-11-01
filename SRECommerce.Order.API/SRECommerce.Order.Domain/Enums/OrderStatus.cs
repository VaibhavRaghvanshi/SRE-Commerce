namespace SRECommerce.Order.Domain;
public enum OrderStatus { AwaitingPayment, Confirmed, Processing, PartiallyFulfilled, Fulfilled, Delivered, Completed, Cancelled, Failed, ManualReview, CancellationPending }
