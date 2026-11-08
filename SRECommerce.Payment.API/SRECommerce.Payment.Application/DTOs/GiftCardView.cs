namespace SRECommerce.Payment.Application;

public sealed record IssueGiftCardRequest(Guid Id, string Code, decimal Value, string Reference);
public sealed record ClaimGiftCardRequest(string Code);
public sealed record GiftCardView(Guid Id, string Currency, decimal Value, decimal Balance,
    DateTimeOffset IssuedAt, DateTimeOffset? ClaimedAt, DateTimeOffset? VoidedAt, string? Reference);
