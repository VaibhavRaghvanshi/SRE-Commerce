using SRECommerce.Tenant.Domain;
using Xunit;
namespace SRECommerce.Tenant.Tests;
public sealed class TenantRuleTests
{
    [Theory]
    [InlineData("http://example.com")][InlineData("127.0.0.1")][InlineData("*.example.com")][InlineData("bad..example.com")][InlineData("-shop.example.com")]
    public void Unsafe_domain_names_are_rejected(string host)=>Assert.Throws<TenantRuleException>(()=>TenantRules.Domain(host));
    [Fact] public void Idn_and_case_are_normalized()=>Assert.Equal("xn--bcher-kva.example",TenantRules.Domain("BÜCHER.example."));
    [Fact] public void Lifecycle_disallows_direct_draft_activation_and_requires_suspend_before_archive()
    {var tenant=new BusinessTenant();Assert.Throws<TenantRuleException>(()=>tenant.Activate());tenant.Submit();tenant.Activate();tenant.Suspend("Test reason");Assert.Equal(TenantStatus.Suspended,tenant.Status);tenant.Activate();Assert.Null(tenant.SuspensionReason);}
    [Fact] public void Cancelled_and_expired_subscriptions_are_ineffective()
    {var now=DateTimeOffset.UtcNow;var subscription=new TenantSubscription{StartsAt=now.AddDays(-1),EndsAt=now.AddDays(1),Status=SubscriptionStatus.Active};Assert.True(subscription.IsEffective(now));subscription.Status=SubscriptionStatus.Cancelled;Assert.False(subscription.IsEffective(now));subscription.Status=SubscriptionStatus.Active;Assert.False(subscription.IsEffective(now.AddDays(2)));}
}
