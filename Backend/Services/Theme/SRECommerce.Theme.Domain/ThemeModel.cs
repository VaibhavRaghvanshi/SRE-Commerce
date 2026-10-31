using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Theme.Domain;

public sealed record ThemePalette(Dictionary<string,string> Colors);
public sealed record DeviceTypography(string Font,int FontSize,int Radius,string Spacing,decimal HeadingScale=1.25m,decimal LineHeight=1.5m);
public sealed record ThemeDocument(int SchemaVersion,ThemePalette Light,ThemePalette Dark,DeviceTypography Desktop,DeviceTypography Mobile,string DefaultMode);
public static partial class ThemeRules
{
    public static readonly string[] ColorKeys=["primary","secondary","accent","background","surface","text","mutedText","border","headerBackground","headerText","footerBackground","footerText","buttonBackground","buttonText","cardBackground"];
    public static readonly string[] Fonts=["system","inter","arial","georgia"];
    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]private static partial Regex ColorPattern();
    public static void Validate(ThemeDocument document)
    {
        if(document is null||document.SchemaVersion!=1||document.DefaultMode is not("light"or"dark"or"system"))throw new DomainFault("InvalidThemeSchema");
        foreach(var palette in new[]{document.Light,document.Dark})
        {
            if(palette?.Colors is null||palette.Colors.Count!=ColorKeys.Length||ColorKeys.Any(key=>!palette.Colors.TryGetValue(key,out var color)||color is null||!ColorPattern().IsMatch(color)))throw new DomainFault("InvalidThemeColors");
            foreach(var (foreground,background) in new[]{("text","background"),("text","surface"),("mutedText","background"),("headerText","headerBackground"),("footerText","footerBackground"),("buttonText","buttonBackground")})
                if(Contrast(palette.Colors[foreground],palette.Colors[background])<4.5)throw new DomainFault("InsufficientThemeContrast");
        }
        foreach(var device in new[]{document.Desktop,document.Mobile})
            if(device is null||!Fonts.Contains(device.Font)||device.FontSize is <12 or >24||device.Radius is <0 or >32||device.Spacing is not("compact"or"normal"or"spacious")||device.HeadingScale is <1.1m or >1.8m||device.LineHeight is <1.2m or >2m)throw new DomainFault("InvalidThemeTypography");
    }
    public static double Contrast(string a,string b)
    {
        static double Luminance(string color)
        {
            double Channel(int offset){var value=Convert.ToInt32(color.Substring(offset,2),16)/255.0;return value<=0.04045?value/12.92:Math.Pow((value+0.055)/1.055,2.4);}
            return .2126*Channel(1)+.7152*Channel(3)+.0722*Channel(5);
        }
        var left=Luminance(a);var right=Luminance(b);return(Math.Max(left,right)+.05)/(Math.Min(left,right)+.05);
    }
    public static Dictionary<string,string> Variables(ThemeDocument document,string mode,bool mobile)
    {
        Validate(document);var colors=mode=="dark"?document.Dark.Colors:document.Light.Colors;var device=mobile?document.Mobile:document.Desktop;
        var values=colors.ToDictionary(pair=>"--color-"+Regex.Replace(pair.Key,"([a-z])([A-Z])","$1-$2").ToLowerInvariant(),pair=>pair.Value,StringComparer.Ordinal);
        values["--font-family"]=device.Font switch{"inter"=>"Inter, sans-serif","arial"=>"Arial, sans-serif","georgia"=>"Georgia, serif",_=>"system-ui, sans-serif"};
        values["--font-size"]=device.FontSize+"px";values["--border-radius"]=device.Radius+"px";values["--heading-scale"]=device.HeadingScale.ToString(System.Globalization.CultureInfo.InvariantCulture);values["--line-height"]=device.LineHeight.ToString(System.Globalization.CultureInfo.InvariantCulture);values["--spacing-scale"]=device.Spacing switch{"compact"=>"0.75","spacious"=>"1.25",_=>"1"};return values;
    }
    public static ThemeDocument Default(string businessType)
    {
        var primary=businessType switch{"spices"=>"#7a3e1d","clothing"=>"#202020","grocery"=>"#166534",_=>"#334155"};
        var light=ColorKeys.ToDictionary(key=>key,key=>key switch{"primary"or"secondary"or"buttonBackground"=>primary,"accent"=>"#b45309","text"or"headerText"or"footerText"=>"#111827","mutedText"=>"#475569","border"=>"#cbd5e1","buttonText"=>"#ffffff",_=>"#ffffff"});
        var dark=ColorKeys.ToDictionary(key=>key,key=>key switch{"primary"or"secondary"=>"#93c5fd","accent"=>"#fbbf24","text"or"headerText"or"footerText"or"buttonText"=>"#f9fafb","mutedText"=>"#cbd5e1","border"=>"#64748b","buttonBackground"=>"#334155","surface"or"cardBackground"=>"#1f2937",_=>"#111827"});
        return new(1,new(light),new(dark),new("system",16,8,"normal"),new("system",16,8,"normal"),"system");
    }
}
public sealed class ThemeDraft:ITenantRecord,IVersionedRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public string Name{get;set;}="";public string Document{get;set;}="";
    public bool Archived{get;set;}public DateTimeOffset UpdatedAt{get;set;}=DateTimeOffset.UtcNow;public byte[] Version{get;set;}=[];
}
public sealed class ThemeVersion:ITenantRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public Guid ThemeId{get;set;}public int Revision{get;set;}
    public string Document{get;set;}="";public string Hash{get;set;}="";public DateTimeOffset CreatedAt{get;set;}=DateTimeOffset.UtcNow;
}
public sealed class TenantTheme:ITenantRecord,IVersionedRecord
{
    public Guid TenantId{get;set;}public Guid ThemeVersionId{get;set;}public long Sequence{get;set;}public byte[] Version{get;set;}=[];
}
public sealed class ThemePublication:ITenantRecord
{
    public Guid TenantId{get;set;}public Guid Id{get;set;}=Guid.NewGuid();public Guid ThemeVersionId{get;set;}public long Sequence{get;set;}public bool Rollback{get;set;}public DateTimeOffset PublishedAt{get;set;}=DateTimeOffset.UtcNow;
}
public sealed class ThemePreset:IVersionedRecord
{
    public string BusinessType{get;set;}="neutral";public string Document{get;set;}="";public DateTimeOffset UpdatedAt{get;set;}=DateTimeOffset.UtcNow;public byte[] Version{get;set;}=[];
}
