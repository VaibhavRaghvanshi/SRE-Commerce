namespace SRECommerce.Identity.API;
public static class IdentityBrowserPolicy
{
    public static string FormAction(IConfiguration config,IHostEnvironment environment)
    {
        var candidates=(config.GetSection("Identity:FormRedirectOrigins").Get<string[]>()??[])
            .Concat(config.GetSection("Cors:Origins").Get<string[]>()??[])
            .Concat(environment.IsDevelopment()?["http://localhost:4200","http://localhost:4000","http://127.0.0.1:5099"]:[]);
        if(config["Client:RedirectUri"] is {} configured)candidates=candidates.Append(configured);
        var origins=new HashSet<string>(StringComparer.Ordinal){"'self'"};
        foreach(var candidate in candidates)
        {
            if(!Uri.TryCreate(candidate,UriKind.Absolute,out var uri)||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)||!string.IsNullOrEmpty(uri.Query)||uri.Scheme!="https"&&(!environment.IsDevelopment()||!new[]{"localhost","127.0.0.1"}.Contains(uri.Host)))throw new InvalidOperationException("Invalid trusted browser redirect origin");
            origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }
        return string.Join(' ',origins.Order(StringComparer.Ordinal));
    }
}
