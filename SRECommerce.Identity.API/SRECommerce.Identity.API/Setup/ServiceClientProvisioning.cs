using System.Text.Json;
using OpenIddict.Abstractions;
using SRECommerce.BuildingBlocks.Contracts;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace SRECommerce.Identity.API;
public static class ServiceClientProvisioning
{
    public static async Task RegisterAsync(IServiceProvider services,string path,CancellationToken ct=default)
    {
        var seeds=JsonSerializer.Deserialize<Dictionary<string,string>>(await File.ReadAllTextAsync(path,ct))??throw new InvalidOperationException("Invalid private service-client manifest");
        var manager=services.GetRequiredService<IOpenIddictApplicationManager>();var count=0;
        foreach(var pair in seeds)
        {
            if(!ServiceManifest.All.Any(s=>s.Name==pair.Key)||ServiceManifest.Scopes(pair.Key).Length==0||pair.Value.Length is <32 or >128)throw new InvalidOperationException("Invalid service-client configuration; no credential values are logged");
            var permissions=new HashSet<string>([Permissions.Endpoints.Token,Permissions.GrantTypes.ClientCredentials],StringComparer.Ordinal);
            foreach(var scope in ServiceManifest.Scopes(pair.Key))permissions.Add(Permissions.Prefixes.Scope+scope);
            var id=ServiceManifest.ClientId(pair.Key);var existing=await manager.FindByClientIdAsync(id,ct);
            if(existing is null)
            {
                var descriptor=new OpenIddictApplicationDescriptor{ClientId=id,ClientSecret=pair.Value,ClientType=ClientTypes.Confidential,DisplayName="SRECommerce "+pair.Key+" service identity"};
                descriptor.Permissions.UnionWith(permissions);await manager.CreateAsync(descriptor,ct);
            }
            else
            {
                if(!await manager.ValidateClientSecretAsync(existing,pair.Value,ct))throw new InvalidOperationException("Existing service secret differs; rotation was not performed");
                if(!permissions.SetEquals(await manager.GetPermissionsAsync(existing,ct)))throw new InvalidOperationException("Service permission drift requires an explicit reviewed update");
            }
            count++;
        }
        Console.WriteLine($"Service clients verified/provisioned: {count}; existing secrets were preserved; no user accounts or passwords changed.");
    }
}
