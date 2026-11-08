namespace SRECommerce.Payment.Application;
public sealed record OrderPaymentProof(Guid Id, Guid TenantId, Guid? UserId, string Currency, decimal Total, string PaymentMethod, int Status, int PaymentState, bool Historical, int DecimalPlaces = 2, Guid? GiftCardId = null);
