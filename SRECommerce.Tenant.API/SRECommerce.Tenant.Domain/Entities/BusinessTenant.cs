using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;

namespace SRECommerce.Tenant.Domain;
public sealed class BusinessTenant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerUserId { get; set; }
    public Guid RegistrationRequestId { get; set; }
    public string RegistrationFingerprint { get; set; } = "";
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string BusinessTypeCode { get; set; } = "";
    public string? CustomBusinessType { get; set; }
    public TenantStatus Status { get; set; } = TenantStatus.Draft;
    public DatabaseStrategy DatabaseStrategy { get; set; } = DatabaseStrategy.Shared;
    public string? SuspensionReason { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] Version { get; set; } = [];
    public void Submit() { if(Status!=TenantStatus.Draft) throw new TenantRuleException("NotDraft"); Status=TenantStatus.PendingApproval; }
    public void Activate()
    {
        if(Status is not (TenantStatus.PendingApproval or TenantStatus.Suspended)) throw new TenantRuleException("CannotActivate");
        Status=TenantStatus.Active; SuspensionReason=null;
    }
    public void Suspend(string reason)
    {
        if(Status!=TenantStatus.Active) throw new TenantRuleException("NotActive");
        SuspensionReason=TenantRules.Name(reason,300); Status=TenantStatus.Suspended;
    }
    public void EnsureEditable() { if(Status==TenantStatus.Archived) throw new TenantRuleException("TenantArchived"); }
}
