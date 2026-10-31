using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;

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
