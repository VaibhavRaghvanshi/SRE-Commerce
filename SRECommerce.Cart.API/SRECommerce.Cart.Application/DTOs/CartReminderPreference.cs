namespace SRECommerce.Cart.Application;

public sealed record CartReminderPreference(bool Available, bool Enabled, bool Queued);
public sealed record CartReminderRequest([property: System.Text.Json.Serialization.JsonRequired] bool Enabled);
