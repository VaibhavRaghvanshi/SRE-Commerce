using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;

public sealed class TenantRuleException(string code, int status = 409) : Exception(code)
{
    public string Code { get; } = code;
    public int Status { get; } = status;
}
public enum TenantStatus { Draft, PendingApproval, Active, Suspended, Archived }
public enum SubscriptionStatus { Trial, Active, Cancelled }
public enum DatabaseStrategy { Shared, Dedicated }

public static partial class TenantRules
{
    public static readonly string[] Modules = ["themes", "storefront", "catalog", "inventory", "pricing", "cart", "orders", "payments", "shipping", "notifications", "manufacturing", "discovery"];
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")] private static partial Regex SlugPattern();
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$")] private static partial Regex DomainLabelPattern();
    public static string Slug(string value)
    {
        var result = value.Trim().ToLowerInvariant();
        if (result.Length is < 3 or > 63 || !SlugPattern().IsMatch(result)) throw new TenantRuleException("InvalidSlug", 400);
        return result;
    }
    public static string Name(string value, int max=160)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length>max || value.Any(char.IsControl)) throw new TenantRuleException("InvalidName", 400);
        return value.Trim();
    }
    public static string Domain(string value)
    {
        string result;
        try { result = new IdnMapping().GetAscii(value.Trim().TrimEnd('.')).ToLowerInvariant(); }
        catch (ArgumentException) { throw new TenantRuleException("InvalidDomain", 400); }
        if (result.Length>253 || !result.Contains('.') || IPAddress.TryParse(result,out _) || result.EndsWith(".localhost",StringComparison.Ordinal)
            || result.Split('.').Any(label=>label.Length is <1 or >63 || !DomainLabelPattern().IsMatch(label))) throw new TenantRuleException("InvalidDomain",400);
        return result;
    }
    public static string? Logo(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (value.Length>500 || !Uri.TryCreate(value,UriKind.Absolute,out var uri) || uri.Scheme!="https" || !string.IsNullOrEmpty(uri.UserInfo)) throw new TenantRuleException("InvalidLogoUrl",400);
        return uri.AbsoluteUri;
    }
}
public sealed class BusinessTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public Guid RegistrationRequestId { get; set; }
    public string RegistrationFingerprint { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string BusinessTypeCode { get; set; } = "";
    public string? CustomBusinessType { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Draft;
    public DatabaseStrategy DatabaseStrategy { get; set; } = DatabaseStrategy.Shared;
    public string? SuspensionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] Version { get; set; } = [];
    public void Submit() { if(Status!=TenantStatus.Draft) throw new TenantRuleException("NotDraft"); Status=TenantStatus.PendingApproval; }
    public void Activate()
    {
        if(Status is not (TenantStatus.PendingApproval or TenantStatus.Suspended)) throw new TenantRuleException("CannotActivate");
        Status=TenantStatus.Active; SuspensionReason=null;
    }
    public void Suspend(string reason)
    {
        if(Status!=TenantStatus.Active) throw new TenantRuleException("NotActive");
        SuspensionReason=TenantRules.Name(reason,300); Status=TenantStatus.Suspended;
    }
    public void EnsureEditable() { if(Status==TenantStatus.Archived) throw new TenantRuleException("TenantArchived"); }
}
public sealed class BusinessType
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}
public interface ITenantRecord { Guid TenantId { get; set; } }
public sealed class TenantSettings : ITenantRecord
{
    public Guid TenantId { get; set; }
    public string Currency { get; set; } = "INR";
    public string Culture { get; set; } = "en-IN";
    public string TimeZone { get; set; } = "Asia/Kolkata";
    public string DisplayName { get; set; } = "";
    public string? LogoUrl { get; set; }
    public void Configure(string currency,string culture,string timeZone,string displayName,string? logo)
    {
        if(currency.Length!=3 || currency.Any(c=>c is <'A' or >'Z')) throw new TenantRuleException("InvalidCurrency",400);
        try { if(CultureInfo.GetCultureInfo(culture).Equals(CultureInfo.InvariantCulture)) throw new CultureNotFoundException(); }
        catch(CultureNotFoundException) { throw new TenantRuleException("InvalidCulture",400); }
        if(!TimeZoneInfo.TryFindSystemTimeZoneById(timeZone,out _)) throw new TenantRuleException("InvalidTimeZone",400);
        Currency=currency; Culture=culture; TimeZone=timeZone; DisplayName=TenantRules.Name(displayName); LogoUrl=TenantRules.Logo(logo);
    }
}
public sealed class TenantModule : ITenantRecord
{
    public Guid TenantId { get; set; }
    public string Module { get; set; } = "";
    public bool Enabled { get; set; }
}
public sealed class SubscriptionPlan
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public int MaxLocations { get; set; }
    public int MaxDomains { get; set; }
    public int MaxUsers { get; set; }
}
public sealed class PlanFeature
{
    public string PlanCode { get; set; } = "";
    public string Feature { get; set; } = "";
    public bool Enabled { get; set; }
}
public sealed class TenantSubscription : ITenantRecord
{
    public Guid TenantId { get; set; }
    public string PlanCode { get; set; } = "";
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public bool IsEffective(DateTimeOffset now) => Status is SubscriptionStatus.Trial or SubscriptionStatus.Active && StartsAt<=now && EndsAt>now;
}
public sealed class TenantDomain : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Host { get; set; } = "";
    public string VerificationHash { get; set; } = "";
    public DateTimeOffset VerificationExpiresAt { get; set; }
    public DateTimeOffset? VerifiedAt { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;
}
public sealed class TenantLocation : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public string CountryCode { get; set; } = "IN";
    public bool IsActive { get; set; } = true;
}
public sealed class TenantAudit : ITenantRecord
{
    public long Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public bool PlatformOperation { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
public sealed class TenantOutbox : ITenantRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? DispatchedAt { get; set; }
}
public sealed class TenantPlatformAudit
{
    public long Id { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = "";
    public string Resource { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
