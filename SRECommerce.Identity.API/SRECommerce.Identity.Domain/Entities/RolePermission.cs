namespace SRECommerce.Identity.Domain;

public sealed class RolePermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Role { get; set; } = "";
    public string Permission { get; set; } = "";
}
