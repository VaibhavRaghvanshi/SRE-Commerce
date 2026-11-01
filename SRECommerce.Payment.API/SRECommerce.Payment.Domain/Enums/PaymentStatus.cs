namespace SRECommerce.Payment.Domain;
// Values are part of the Order HTTP contract.
public enum PaymentStatus { Pending, Authorized, Captured, Cancelled, Failed, PartiallyRefunded, Refunded, CashOnDelivery }
