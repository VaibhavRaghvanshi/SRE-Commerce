using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Notification.Domain;

namespace SRECommerce.Notification.Application;

public static class NotificationTemplateRules
{
    private static readonly Regex Tokens = new(@"\{\{\s*(?<name>[A-Za-z]+)\s*\}\}", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    public static string Validate(string value, NotificationKind kind, bool subject)
    {
        var inputLimit = subject ? 160 : 2000;
        if (string.IsNullOrWhiteSpace(value) || value.Length > inputLimit ||
            value.Any(c => char.IsControl(c) && (subject || c is not ('\r' or '\n' or '\t'))))
            throw new BusinessFault("InvalidNotificationTemplate", 400);
        var allowed = kind == NotificationKind.LowStock
            ? new Dictionary<string, int> { ["sku"] = 64, ["available"] = 32, ["reorderLevel"] = 32 }
            : new Dictionary<string, int> { ["orderNumber"] = 80 };
        var matches = Tokens.Matches(value);
        var literal = Tokens.Replace(value, "");
        if (literal.Contains('{') || literal.Contains('}') || matches.Count > (kind == NotificationKind.LowStock ? 4 : 1) ||
            matches.Any(match => !allowed.ContainsKey(match.Groups["name"].Value)))
            throw new BusinessFault("UnknownNotificationTemplateToken", 400);
        if (literal.Length + matches.Sum(match => allowed[match.Groups["name"].Value]) > (subject ? 200 : 2400))
            throw new BusinessFault("RenderedNotificationTooLong", 400);
        return value.Trim();
    }
    public static string Render(string template, IReadOnlyDictionary<string, string> values) =>
        Tokens.Replace(template, match => values[match.Groups["name"].Value]);
}
