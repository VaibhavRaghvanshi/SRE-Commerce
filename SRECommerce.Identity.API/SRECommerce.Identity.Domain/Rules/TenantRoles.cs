namespace SRECommerce.Identity.Domain;

public static class TenantRoles
{
    public static readonly string[] All = ["Owner", "Manager", "Staff", "Customer", "B2BBuyer"];
    public static string[] Permissions(string role) => role switch
    {
        "Owner" or "Manager" => ["members.read", "members.write"],
        "Staff" => ["members.read"],
        _ => []
    };
}
