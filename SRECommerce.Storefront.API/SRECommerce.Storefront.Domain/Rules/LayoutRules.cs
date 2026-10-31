using System.Text.RegularExpressions;
using SRECommerce.BuildingBlocks.Kernel;

namespace SRECommerce.Storefront.Domain;
public static partial class LayoutRules
{
    public static readonly string[] SectionKinds=["hero","featured-categories","featured-products","new-arrivals","best-sellers","promotion","reviews","text","product-fields"];
    public static readonly string[] FieldNames=["weight","origin","expiry","ingredients","nutrition","size","color","fabric","fit","sizeChart","brand","warranty","material","dimensions","delivery","repeatOrder"];
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]private static partial Regex SlugPattern();
    public static string Slug(string value)
    {
        var slug=DomainValues.Text(value,80).ToLowerInvariant();
        if(!SlugPattern().IsMatch(slug)||new[]{"api","internal","account","login","checkout","cart","search","products","categories","platform","admin","health"}.Contains(slug))throw new DomainFault("InvalidPageSlug");return slug;
    }
    public static void Url(string value,bool media=false)
    {
        if(string.IsNullOrWhiteSpace(value)||value.Length>1024||value.Any(char.IsControl)||value.Contains('\\')||value.Contains('"')||value.Contains('\'')||value.Contains('<')||value.Contains('>')||value.Contains('%'))throw new DomainFault("InvalidLayoutUrl");
        if(value.StartsWith('/')&&!value.StartsWith("//")&&!value.Contains(".."))
        {
            if(media&&!value.StartsWith("/uploads/",StringComparison.Ordinal))throw new DomainFault("InvalidMediaPath");
            if(!media&&value!="/"&&!new[]{"/products","/categories","/pages","/cart","/search"}.Any(p=>value==p||value.StartsWith(p+"/",StringComparison.Ordinal)||value.StartsWith(p+"?",StringComparison.Ordinal)))throw new DomainFault("UnsupportedNavigationPath");
            return;
        }
        if(!Uri.TryCreate(value,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo))throw new DomainFault("InvalidLayoutUrl");
    }
    private static void Plain(string? value,int maximum,bool optional=false)
    {if(optional&&string.IsNullOrEmpty(value))return;DomainValues.Text(value!,maximum);if(value!.Contains('<')||value.Contains('>'))throw new DomainFault("HtmlNotSupported");}
    private static void Placement(DevicePlacement? value)
    {if(value is null||value.Order is <1 or >100||value.Columns is <1 or >6)throw new DomainFault("InvalidDevicePlacement");}
    private static void Unique<T>(IEnumerable<T> values)
    {var all=values.ToArray();if(all.Length!=all.Distinct().Count())throw new DomainFault("DuplicateLayoutIdentifier");}
    public static void Validate(LayoutDocument document)
    {
        if(document is null||document.SchemaVersion!=1||document.Template is not("neutral"or"spices"or"clothing"or"grocery"or"general-store"or"custom")||document.Header is null||document.Footer is null||document.Product is null||document.Menus is null||document.Banners is null||document.Sections is null||document.Product.BusinessFields is null)throw new DomainFault("InvalidLayoutSchema");
        if(document.Sections.Length is <1 or >30||document.Menus.Length>3||document.Banners.Length>10||document.Product.BusinessFields.Length>FieldNames.Length||document.Product.BusinessFields.Any(f=>!FieldNames.Contains(f)))throw new DomainFault("InvalidLayoutCollection");
        Unique(document.Product.BusinessFields);Plain(document.Footer.Copyright,200);Plain(document.Footer.ContactText,500,true);
        if(document.Menus.Any(m=>m is null)||document.Banners.Any(b=>b is null)||document.Sections.Any(s=>s is null))throw new DomainFault("InvalidLayoutItem");
        Unique(document.Menus.Select(m=>m.Position));Unique(document.Banners.Select(b=>b.Id));Unique(document.Sections.Select(s=>s.Id));
        foreach(var menu in document.Menus)
        {
            if(menu.Position is not("header"or"footer"or"mobile")||menu.Links is null||menu.Links.Length>30||menu.Links.Any(l=>l is null))throw new DomainFault("InvalidNavigation");Unique(menu.Links.Select(l=>l.Id));
            foreach(var link in menu.Links){if(link.Id==Guid.Empty)throw new DomainFault("InvalidLayoutIdentifier");Plain(link.Label,80);Url(link.Href);}
        }
        foreach(var banner in document.Banners)
        {if(banner.Id==Guid.Empty)throw new DomainFault("InvalidLayoutIdentifier");Plain(banner.Title,160);Plain(banner.Alt,200);Url(banner.ImageUrl,true);Url(banner.Link);Placement(banner.Desktop);Placement(banner.Mobile);}
        foreach(var section in document.Sections)
        {
            if(section.Id==Guid.Empty||!SectionKinds.Contains(section.Kind)||section.References is null||section.References.Length>40||section.References.Contains(Guid.Empty))throw new DomainFault("InvalidLayoutSection");
            Unique(section.References);Plain(section.Title,160);Plain(section.Text,2000,true);Placement(section.Desktop);Placement(section.Mobile);
        }
        Unique(document.Sections.Where(s=>s.Desktop.Visible).Select(s=>s.Desktop.Order));Unique(document.Sections.Where(s=>s.Mobile.Visible).Select(s=>s.Mobile.Order));
    }
    public static LayoutDocument Default(string businessType)
    {
        var type=businessType is "spices"or"clothing"or"grocery"or"general-store"or"custom"?businessType:"neutral";
        var fields=type switch{"spices"=>new[]{"weight","origin","expiry","ingredients"},"clothing"=>["size","color","fabric","fit","sizeChart"],"grocery"=>["weight","expiry","delivery"],"general-store"=>["brand","delivery","repeatOrder"],_=>Array.Empty<string>()};
        // Stable bundled IDs keep repeat fallbacks/cache validators deterministic.
        return new(1,type,new(true,true,true,false),new("Store information",true,null),[new("header",[new(Guid.Parse("00000000-0000-0000-0000-000000000001"),"Products","/products")])],[],
        [new(Guid.Parse("00000000-0000-0000-0000-000000000002"),"featured-categories","Shop categories",null,[],new(true,1,4),new(true,1,2)),new(Guid.Parse("00000000-0000-0000-0000-000000000003"),"new-arrivals","New arrivals",null,[],new(true,2,4),new(true,2,2)),new(Guid.Parse("00000000-0000-0000-0000-000000000004"),"product-fields","Product information",null,[],new(true,3,1),new(true,3,1))],new(true,true,true,fields));
    }
}
