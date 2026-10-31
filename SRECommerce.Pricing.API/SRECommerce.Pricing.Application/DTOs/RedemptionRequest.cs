namespace SRECommerce.Pricing.Application;
public record RedemptionRequest(Guid OrderId,Guid QuoteId,Guid? UserId);
