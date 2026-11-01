namespace SRECommerce.Order.Domain;
public enum PaymentState { Pending, Authorized, Captured, CashOnDelivery, PartiallyRefunded, Refunded, Failed, NotRecorded }
