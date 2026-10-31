using System.Text;
using System.Text.RegularExpressions;
namespace SRECommerce.Application.Services;
public static class SlugHelper
{
    public static string ToSlug(string input)
    {
        var s = input.Trim().ToLowerInvariant();
        s = Regex.Replace(s, @"\s+", "-");
        s = Regex.Replace(s, @"[^a-z0-9\-]", "");
        s = Regex.Replace(s, @"-+", "-");
        return s.Trim('-');
    }
}

