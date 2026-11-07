namespace SRECommerce.BuildingBlocks.Contracts;

public sealed record CartReminderEvent(Guid CartId, Guid UserId);
public sealed record CartReminderRecipient(Guid UserId, string Email);
