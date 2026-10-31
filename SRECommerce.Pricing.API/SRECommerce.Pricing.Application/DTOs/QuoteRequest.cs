namespace SRECommerce.Pricing.Application;
public record QuoteRequest(Guid Id,QuoteItem[] Items,string? CouponCode=null);
