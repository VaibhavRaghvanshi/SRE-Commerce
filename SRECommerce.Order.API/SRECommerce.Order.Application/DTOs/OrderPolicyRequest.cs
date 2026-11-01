namespace SRECommerce.Order.Application;
public sealed record OrderPolicyRequest(bool AllowGuest,bool AllowCOD,bool AllowExchanges,int ReturnWindowDays,decimal MaximumTotal,string? Version=null);
