using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
namespace SRECommerce.Identity.API;
public static class AngularClientSetup
{
    public static async Task InitializeAsync(IServiceProvider services,IHostEnvironment environment)
    {
        if(!environment.IsDevelopment())return;
        var manager=services.GetRequiredService<IOpenIddictApplicationManager>();const string id="srecommerce-angular";
        var application=await manager.FindByClientIdAsync(id);var descriptor=new OpenIddictApplicationDescriptor();
        if(application is not null)await manager.PopulateAsync(descriptor,application);
        descriptor.ClientId=id;descriptor.ClientType=ClientTypes.Public;descriptor.ConsentType=ConsentTypes.Implicit;descriptor.DisplayName="SRECommerce Angular development client";
        foreach(var origin in new[]{"http://localhost:4200","http://localhost:4000"})descriptor.RedirectUris.Add(new Uri(origin+"/commerce/callback"));
        descriptor.Permissions.UnionWith([Permissions.Endpoints.Authorization,Permissions.Endpoints.Token,Permissions.Endpoints.Revocation,Permissions.Endpoints.EndSession,Permissions.GrantTypes.AuthorizationCode,Permissions.GrantTypes.RefreshToken,Permissions.ResponseTypes.Code,Permissions.Scopes.Email,Permissions.Scopes.Profile,Permissions.Prefixes.Scope+"identity_api",Permissions.Prefixes.Scope+"tenant_api",Permissions.Prefixes.Scope+"commerce_api"]);
        descriptor.Requirements.Add(Requirements.Features.ProofKeyForCodeExchange);
        if(application is null)await manager.CreateAsync(descriptor);else await manager.UpdateAsync(application,descriptor);
    }
}
