namespace SRECommerce.Pricing.Application;
public record GroupMemberRequest(Guid GroupId,bool Active=true,string? Version=null);
