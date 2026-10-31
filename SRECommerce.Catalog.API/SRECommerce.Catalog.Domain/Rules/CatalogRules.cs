using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;
namespace SRECommerce.Catalog.Domain;
public static partial class CatalogRules
{
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")] private static partial Regex SlugPattern();
    [GeneratedRegex("^[A-Z0-9][A-Z0-9._-]{0,63}$")] private static partial Regex SkuPattern();
    public static string Slug(string value)
    {
        var result = DomainValues.Text(value,120).ToLowerInvariant();
        if (!SlugPattern().IsMatch(result)) throw new DomainFault("InvalidCatalogSlug");
        return result;
    }
    public static string Sku(string value)
    {
        var result=DomainValues.Text(value,64).ToUpperInvariant();
        if(!SkuPattern().IsMatch(result)) throw new DomainFault("InvalidSku");
        return result;
    }
    public static string Text(string value,int maximum=160,bool optional=false)
    {
        if(optional&&string.IsNullOrWhiteSpace(value))return "";
        if(string.IsNullOrWhiteSpace(value)||value.Length>maximum||value.Any(c=>char.IsControl(c)&&c is not ('\r' or '\n' or '\t'))||value.Contains('<')||value.Contains('>'))throw new DomainFault("InvalidCatalogText");
        return value.Trim();
    }
    public static string MediaUrl(string url)
    {
        if(string.IsNullOrWhiteSpace(url)||url.Length>1024||url.Any(char.IsControl)||url.Contains('\\')||url.Contains('"')||url.Contains('\''))throw new DomainFault("InvalidMediaUrl");
        if((url.StartsWith("/uploads/",StringComparison.Ordinal)||url.StartsWith("/assets/Images/",StringComparison.Ordinal))&&!url.Contains("..")&&!url.Contains('%'))return url;
        if(!Uri.TryCreate(url,UriKind.Absolute,out var value)||value.Scheme!="https"||!string.IsNullOrEmpty(value.UserInfo))throw new DomainFault("InvalidMediaUrl");
        return url;
    }
    public static void Attributes(Dictionary<string,JsonElement> values,IReadOnlyCollection<(AttributeDefinition Definition,bool Required)> bindings,bool publishing)
    {
        if(values is null||values.Count>60)throw new DomainFault("InvalidAttributes");
        var definitions=bindings.ToDictionary(b=>b.Definition.Code,StringComparer.Ordinal);
        foreach(var (key,value) in values)
        {
            if(!definitions.TryGetValue(key,out var binding)||binding.Definition.Archived)throw new DomainFault("AttributeNotAllowedForCategory");
            var definition=binding.Definition;
            var valid=definition.DataType switch
            {
                AttributeDataType.Text=>value.ValueKind==JsonValueKind.String&&value.GetString()!.Length is >0 and <=500,
                AttributeDataType.Number=>value.ValueKind==JsonValueKind.Number&&value.TryGetDecimal(out var number)&&number is >=-1000000000000m and <=1000000000000m,
                AttributeDataType.Boolean=>value.ValueKind is JsonValueKind.True or JsonValueKind.False,
                AttributeDataType.Date=>value.ValueKind==JsonValueKind.String&&DateOnly.TryParseExact(value.GetString(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _),
                AttributeDataType.Choice=>value.ValueKind==JsonValueKind.String&&(JsonSerializer.Deserialize<string[]>(definition.Options)??[]).Contains(value.GetString(),StringComparer.Ordinal),
                _=>false
            };
            if(!valid)throw new DomainFault("AttributeValueTypeMismatch");
        }
        if(publishing&&bindings.Any(b=>b.Required&&!values.ContainsKey(b.Definition.Code)))throw new DomainFault("RequiredAttributeMissing");
    }
    public static Dictionary<string,string> VariantOptions(Dictionary<string,string> options)
    {
        if(options is null||options.Count>8)throw new DomainFault("InvalidVariantOptions");
        return options.OrderBy(p=>p.Key,StringComparer.Ordinal).ToDictionary(p=>Slug(p.Key),p=>Text(p.Value,80),StringComparer.Ordinal);
    }
    public static void Specification(Dictionary<string,string> values)
    {if(values is null||values.Count>60)throw new DomainFault("InvalidSpecifications");foreach(var pair in values){Text(pair.Key,80);Text(pair.Value,1000);}}
}
