using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
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
