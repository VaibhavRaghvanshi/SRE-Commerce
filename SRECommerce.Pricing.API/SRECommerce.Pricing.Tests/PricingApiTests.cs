using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Pricing.Application;
using SRECommerce.Pricing.Domain;
using SRECommerce.Pricing.Infrastructure;
using Xunit;
using static SRECommerce.Pricing.Tests.PricingFixture;
namespace SRECommerce.Pricing.Tests;
public sealed class PricingApiTests(PricingFixture fixture):IClassFixture<PricingFixture>
{
    private static QuoteRequest Request(PricingBusiness b,decimal quantity=1,string? coupon=null)=>new(Guid.NewGuid(),[new(b.Variant,quantity)],coupon);
    private async Task<(Guid Promotion,Guid Coupon)> Coupon(PricingBusiness b,int limit=1,int perCustomer=0)
    {
        var now=fixture.Factory.Clock.GetUtcNow();var promotion=await Read<JsonElement>(await b.Client.PostAsJsonAsync(Path(b.Catalog.Id)+"/promotions",new PromotionRequest("TEN","Ten percent",PromotionKind.Percentage,10,0,0,0,null,[],null,false,false,0,now.AddDays(-1),now.AddDays(30))),HttpStatusCode.Created);
        var coupon=await Read<JsonElement>(await b.Client.PostAsJsonAsync(Path(b.Catalog.Id)+"/coupons",new CouponRequest("SAVE10",promotion.GetProperty("id").GetGuid(),limit,perCustomer)),HttpStatusCode.Created);return(promotion.GetProperty("id").GetGuid(),coupon.GetProperty("id").GetGuid());
    }
    [Fact]
    public async Task Price_imports_are_atomic_replay_safe_and_preserve_unknown_cost()
    {
        var b=await fixture.Business();using var client=b.Client;using var catalog=b.Catalog.Client;var path=Path(b.Catalog.Id);
        var row=new PriceRequest(b.List,b.Product,b.Variant,b.Tax,2,120,90,null);var request=new PriceImportRequest(Guid.NewGuid(),"legacy-source",[row,row with{MinimumQuantity=3,TaxPolicyId=Guid.NewGuid()}]);
        Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/imports",request)).StatusCode);
        var before=(await client.GetFromJsonAsync<PricePage<ProductPriceView>>(path+"/prices"))!;Assert.Single(before.Items);
        request=request with{Prices=[row,row with{MinimumQuantity=3,SellingPrice=80}]};var ids=await Read<Guid[]>(await client.PostAsJsonAsync(path+"/imports",request));Assert.Equal(2,ids.Length);
        Assert.Equal(ids,await Read<Guid[]>(await client.PostAsJsonAsync(path+"/imports",request)));Assert.Equal(HttpStatusCode.Conflict,(await client.PostAsJsonAsync(path+"/imports",request with{Source="different"})).StatusCode);
        using var anonymous=fixture.Factory.Client();Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(path+"/imports",request)).StatusCode);
        var prices=(await client.GetFromJsonAsync<PricePage<ProductPriceView>>(path+"/prices"))!;Assert.Equal(3,prices.Total);Assert.All(prices.Items.Where(p=>ids.Contains(p.Id)),p=>Assert.Null(p.CostPrice));
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PricingDbContext>();Assert.Equal(2,await db.History.IgnoreQueryFilters().CountAsync(h=>h.TenantId==b.Catalog.Id&&ids.Contains(h.PriceId)));
    }
    [Fact]
    public async Task Public_quotes_use_server_tiers_tax_and_immutable_idempotent_snapshots_without_cost_leaks()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var path=Path(b.Catalog.Id);
        var lookups=await owner.GetFromJsonAsync<JsonElement>(path+"/lookups");var tax=lookups.GetProperty("taxes")[0];
        await Read<JsonElement>(await owner.PutAsJsonAsync(path+$"/tax-policies/{b.Tax}",new TaxRequest("NONE","Configured test tax",18,false,Version:tax.GetProperty("version").GetString())));
        await Read<ProductPriceView>(await owner.PostAsJsonAsync(path+"/prices",new PriceRequest(b.List,b.Product,b.Variant,b.Tax,10,120,80,50)),HttpStatusCode.Created);
        using var anonymous=fixture.Factory.Client();var request=Request(b,10);var quote=await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",request));Assert.Equal(944,quote.Calculation.Total);Assert.Equal(144,quote.Calculation.Tax);Assert.Null(quote.UserId);
        var replay=await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",request));Assert.Equal(quote.Id,replay.Id);Assert.Equal(quote.Calculation.Total,replay.Calculation.Total);
        Assert.Equal(HttpStatusCode.Conflict,(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",request with{Items=[new(b.Variant,1)]})).StatusCode);
        var json=await(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b))).Content.ReadAsStringAsync();Assert.DoesNotContain("costPrice",json,StringComparison.OrdinalIgnoreCase);
        Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.GetAsync(path+"/prices")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await anonymous.PostAsJsonAsync(path+"/prices",new PriceRequest(b.List,b.Product,b.Variant,b.Tax,2,120,1,0))).StatusCode);
    }
    [Fact]
    public async Task Concurrent_coupon_claims_cannot_exceed_capacity_and_redeem_is_idempotent()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;await Coupon(b);using var anonymous=fixture.Factory.Client();
        var a=await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b,coupon:"SAVE10")));var c=await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b,coupon:"SAVE10")));Assert.Equal(90,a.Calculation.Total);
        using var order=fixture.Factory.Client(await fixture.Machine("Order"));var requests=new[]{new RedemptionRequest(Guid.NewGuid(),a.Id,null),new RedemptionRequest(Guid.NewGuid(),c.Id,null)};
        var responses=await Task.WhenAll(requests.Select(r=>order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",r)));Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.OK);Assert.Single(responses,r=>r.StatusCode==HttpStatusCode.Conflict);
        var index=Array.FindIndex(responses,r=>r.StatusCode==HttpStatusCode.OK);var hold=await Read<RedemptionView>(responses[index]);var duplicate=await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",requests[index]));Assert.Equal(hold.OrderId,duplicate.OrderId);
        var redeemed=await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+$"/redemptions/{hold.OrderId}/redeem",new RedemptionVersionRequest(hold.Version)));Assert.Equal(RedemptionStatus.Redeemed,redeemed.Status);
        var repeated=await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+$"/redemptions/{hold.OrderId}/redeem",new RedemptionVersionRequest(hold.Version)));Assert.Equal(RedemptionStatus.Redeemed,repeated.Status);
        Assert.Equal(HttpStatusCode.Conflict,(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b,coupon:"SAVE10"))).StatusCode);
    }
    [Fact]
    public async Task Expired_quotes_and_coupon_holds_cannot_be_committed_and_capacity_recovers()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;await Coupon(b);using var anonymous=fixture.Factory.Client();using var order=fixture.Factory.Client(await fixture.Machine("Order"));
        var request=Request(b,coupon:"SAVE10");var quote=await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",request));var hold=await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",new RedemptionRequest(Guid.NewGuid(),quote.Id,null)));
        fixture.Factory.Clock.Advance(TimeSpan.FromMinutes(16));var expired=await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+$"/redemptions/{hold.OrderId}/redeem",new RedemptionVersionRequest(hold.Version)));Assert.Equal(RedemptionStatus.Expired,expired.Status);
        Assert.Equal(HttpStatusCode.Conflict,(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",request)).StatusCode);
        var fresh=await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b,coupon:"SAVE10")));var second=await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",new RedemptionRequest(Guid.NewGuid(),fresh.Id,null)));Assert.Equal(RedemptionStatus.Reserved,second.Status);
        var released=await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+$"/redemptions/{second.OrderId}/release",new RedemptionVersionRequest(second.Version)));Assert.Equal(RedemptionStatus.Released,released.Status);
    }
    [Fact]
    public async Task Customer_group_prices_come_from_verified_membership_and_group_changes_invalidate_unclaimed_quotes()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var customer=await fixture.Customer(b);using var buyer=customer.Client;var path=Path(b.Catalog.Id);var now=fixture.Factory.Clock.GetUtcNow();
        var group=await Read<JsonElement>(await owner.PostAsJsonAsync(path+"/groups",new GroupRequest("VIP","VIP")),HttpStatusCode.Created);var groupId=group.GetProperty("id").GetGuid();
        await Read<JsonElement>(await owner.PutAsJsonAsync(path+$"/group-members/{customer.Id}",new GroupMemberRequest(groupId)));
        var assigned=await owner.GetFromJsonAsync<JsonElement>(path+$"/group-members/{customer.Id}");Assert.Equal(groupId,assigned.GetProperty("groupId").GetGuid());
        var list=await Read<JsonElement>(await owner.PostAsJsonAsync(path+"/price-lists",new PriceListRequest("VIP","VIP","INR",PriceAudience.CustomerGroup,groupId,0,now.AddDays(-1),now.AddDays(30))),HttpStatusCode.Created);
        await Read<ProductPriceView>(await owner.PostAsJsonAsync(path+"/prices",new PriceRequest(list.GetProperty("id").GetGuid(),b.Product,b.Variant,b.Tax,1,120,70,60)),HttpStatusCode.Created);
        var quote=await Read<QuoteView>(await buyer.PostAsJsonAsync(path+"/quotes",Request(b)));Assert.Equal(70,quote.Calculation.Total);Assert.Equal(customer.Id,quote.UserId);
        using var anonymous=fixture.Factory.Client();Assert.Equal(100,(await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b)))).Calculation.Total);
        await Read<JsonElement>(await owner.PutAsJsonAsync(path+$"/groups/{groupId}",new GroupRequest("VIP","VIP",false,group.GetProperty("version").GetString())));
        using var order=fixture.Factory.Client(await fixture.Machine("Order"));Assert.Equal(HttpStatusCode.Conflict,(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",new RedemptionRequest(Guid.NewGuid(),quote.Id,customer.Id))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,(await owner.PutAsJsonAsync(path+$"/group-members/{Guid.NewGuid()}",new GroupMemberRequest(groupId))).StatusCode);
    }
    [Fact]
    public async Task B2B_prices_and_catalog_visibility_require_a_current_B2B_membership()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var buyer=await fixture.Customer(b,"B2BBuyer");using var wholesale=buyer.Client;var customer=await fixture.Customer(b);using var retail=customer.Client;var now=fixture.Factory.Clock.GetUtcNow();var path=Path(b.Catalog.Id);
        var list=await Read<JsonElement>(await owner.PostAsJsonAsync(path+"/price-lists",new PriceListRequest("WHOLESALE","Wholesale","INR",PriceAudience.B2B,null,0,now.AddDays(-1),now.AddDays(30))),HttpStatusCode.Created);
        await Read<ProductPriceView>(await owner.PostAsJsonAsync(path+"/prices",new PriceRequest(list.GetProperty("id").GetGuid(),b.Product,b.Variant,b.Tax,1,120,75,60)),HttpStatusCode.Created);
        Assert.Equal(75,(await Read<QuoteView>(await wholesale.PostAsJsonAsync(path+"/quotes",Request(b)))).Calculation.Total);Assert.Equal(100,(await Read<QuoteView>(await retail.PostAsJsonAsync(path+"/quotes",Request(b)))).Calculation.Total);
        var product=(await catalog.GetFromJsonAsync<SRECommerce.Catalog.Application.ProductView>($"/api/v1/tenants/{b.Catalog.Id}/catalog/products/{b.Product}"))!;
        var change=new SRECommerce.Catalog.Application.ProductDraft(product.Name,product.Slug,product.CategoryId,product.BrandId,product.Kind,product.Description,product.SeoTitle,product.SeoDescription,true,true,product.Attributes,product.Specifications,product.Variants,product.Media,product.BundleItems,product.Relationships,product.Version);
        product=await Read<SRECommerce.Catalog.Application.ProductView>(await catalog.PutAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/catalog/products/{b.Product}",change));await Read<SRECommerce.Catalog.Application.ProductView>(await catalog.PostAsJsonAsync($"/api/v1/tenants/{b.Catalog.Id}/catalog/products/{b.Product}/publish",new SRECommerce.Catalog.Application.VersionRequest(product.Version!)));
        Assert.Equal(HttpStatusCode.Conflict,(await retail.PostAsJsonAsync(path+"/quotes",Request(b))).StatusCode);Assert.Equal(75,(await Read<QuoteView>(await wholesale.PostAsJsonAsync(path+"/quotes",Request(b)))).Calculation.Total);
    }
    [Fact]
    public async Task Price_history_is_atomic_immutable_and_stale_updates_do_not_create_history()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var path=Path(b.Catalog.Id);var price=Assert.Single((await owner.GetFromJsonAsync<PricePage<ProductPriceView>>(path+"/prices"))!.Items);
        var request=new PriceRequest(b.List,b.Product,b.Variant,b.Tax,1,120,95,55,Version:price.Version);await Read<ProductPriceView>(await owner.PutAsJsonAsync(path+$"/prices/{price.Id}",request));Assert.Equal(HttpStatusCode.Conflict,(await owner.PutAsJsonAsync(path+$"/prices/{price.Id}",request)).StatusCode);
        var history=(await owner.GetFromJsonAsync<JsonElement[]>(path+$"/prices/{price.Id}/history"))!;Assert.Equal(2,history.Length);
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PricingDbContext>();scope.ServiceProvider.GetRequiredService<ModuleScope>().BindEvent(b.Catalog.Id,"Pricing");var row=await db.History.FirstAsync();row.After="{}";await Assert.ThrowsAsync<BusinessFault>(()=>db.SaveChangesAsync());
    }
    [Fact]
    public async Task Scope_boundaries_prevent_human_internal_calls_wrong_services_and_quote_reuse()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var quote=await Read<QuoteView>(await owner.PostAsJsonAsync(Path(b.Catalog.Id)+"/quotes",Request(b)));
        Assert.Equal(HttpStatusCode.Unauthorized,(await owner.GetAsync(InternalPath(b.Catalog.Id)+$"/quotes/{quote.Id}")).StatusCode);
        using var cart=fixture.Factory.Client(await fixture.Machine("Cart"));Assert.Equal(HttpStatusCode.OK,(await cart.GetAsync(InternalPath(b.Catalog.Id)+$"/quotes/{quote.Id}")).StatusCode);Assert.Equal(HttpStatusCode.Forbidden,(await cart.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",new RedemptionRequest(Guid.NewGuid(),quote.Id,quote.UserId))).StatusCode);
        using var order=fixture.Factory.Client(await fixture.Machine("Order"));await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",new RedemptionRequest(Guid.NewGuid(),quote.Id,quote.UserId)));
        Assert.Equal(HttpStatusCode.Conflict,(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",new RedemptionRequest(Guid.NewGuid(),quote.Id,quote.UserId))).StatusCode);
    }
    [Fact]
    public async Task Scheduled_offers_currency_tampering_and_cross_tenant_context_are_checked()
    {
        var a=await fixture.Business();var b=await fixture.Business();using var ac=a.Client;using var bc=b.Client;using var ca=a.Catalog.Client;using var cb=b.Catalog.Client;var now=fixture.Factory.Clock.GetUtcNow();var path=Path(a.Catalog.Id);
        await Read<JsonElement>(await ac.PostAsJsonAsync(path+"/promotions",new PromotionRequest("LATER","Future offer",PromotionKind.Percentage,90,0,0,0,null,[],null,false,true,10,now.AddHours(1),now.AddDays(1))),HttpStatusCode.Created);
        using var anonymous=fixture.Factory.Client();Assert.Equal(100,(await Read<QuoteView>(await anonymous.PostAsJsonAsync(Public(a.Catalog.Id)+"/quotes",Request(a)))).Calculation.Total);
        Assert.Equal(HttpStatusCode.Conflict,(await ac.PostAsJsonAsync(path+"/price-lists",new PriceListRequest("WRONG","Wrong currency","USD",PriceAudience.Retail,null,0,now,now.AddDays(1)))).StatusCode);
        ac.DefaultRequestHeaders.Add("X-Tenant-ID",b.Catalog.Id.ToString());Assert.Equal(HttpStatusCode.Forbidden,(await ac.GetAsync(Path(b.Catalog.Id)+"/prices")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await anonymous.PostAsJsonAsync(Public(a.Catalog.Id)+"/quotes",new{id=Guid.NewGuid(),items=new[]{new{variantId=a.Variant,quantity=1}},discount=99,userId=Guid.NewGuid()})).StatusCode);
    }
    [Fact]
    public async Task Required_dependencies_fail_closed_while_guest_quotes_do_not_require_identity()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;using var anonymous=fixture.Factory.Client();var path=Public(b.Catalog.Id)+"/quotes";
        try
        {
            fixture.Factory.IdentityOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await owner.GetAsync(Path(b.Catalog.Id)+"/prices")).StatusCode);await Read<QuoteView>(await anonymous.PostAsJsonAsync(path,Request(b)));fixture.Factory.IdentityOffline=false;
            fixture.Factory.CatalogOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await anonymous.PostAsJsonAsync(path,Request(b))).StatusCode);
            fixture.Factory.CatalogOffline=false;fixture.Factory.TenantOffline=true;Assert.Equal(HttpStatusCode.ServiceUnavailable,(await anonymous.PostAsJsonAsync(path,Request(b))).StatusCode);
        }
        finally{fixture.Factory.IdentityOffline=false;fixture.Factory.CatalogOffline=false;fixture.Factory.TenantOffline=false;}
    }
    [Fact]
    public async Task Per_customer_coupon_limits_cannot_be_bypassed_by_guest_or_another_quote()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;await Coupon(b,10,1);
        var a=await fixture.Customer(b);var c=await fixture.Customer(b);using var first=a.Client;using var second=c.Client;var path=Path(b.Catalog.Id)+"/quotes";
        var quote=await Read<QuoteView>(await first.PostAsJsonAsync(path,Request(b,coupon:"SAVE10")));using var order=fixture.Factory.Client(await fixture.Machine("Order"));
        await Read<RedemptionView>(await order.PostAsJsonAsync(InternalPath(b.Catalog.Id)+"/redemptions",new RedemptionRequest(Guid.NewGuid(),quote.Id,a.Id)));
        Assert.Equal(HttpStatusCode.Conflict,(await first.PostAsJsonAsync(path,Request(b,coupon:"SAVE10"))).StatusCode);
        Assert.Equal(90,(await Read<QuoteView>(await second.PostAsJsonAsync(path,Request(b,coupon:"SAVE10")))).Calculation.Total);
        using var guest=fixture.Factory.Client();Assert.Equal(HttpStatusCode.BadRequest,(await guest.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b,coupon:"SAVE10"))).StatusCode);
    }
    [Fact]
    public async Task Unknown_source_cost_remains_null_while_public_quotes_stay_authoritative()
    {
        var b=await fixture.Business();using var owner=b.Client;using var catalog=b.Catalog.Client;var path=Path(b.Catalog.Id);var price=Assert.Single((await owner.GetFromJsonAsync<PricePage<ProductPriceView>>(path+"/prices"))!.Items);
        price=await Read<ProductPriceView>(await owner.PutAsJsonAsync(path+$"/prices/{price.Id}",new PriceRequest(b.List,b.Product,b.Variant,b.Tax,1,120,100,null,Version:price.Version)));Assert.Null(price.CostPrice);
        using var guest=fixture.Factory.Client();Assert.Equal(100,(await Read<QuoteView>(await guest.PostAsJsonAsync(Public(b.Catalog.Id)+"/quotes",Request(b)))).Calculation.Total);
    }
    [Fact]
    public async Task Migration_readiness_openapi_and_query_filters_are_verified()
    {
        using var scope=fixture.Factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<PricingDbContext>();await db.Database.MigrateAsync();Assert.Empty(await db.Database.GetPendingMigrationsAsync());Assert.Empty(await db.Prices.ToArrayAsync());
        using var client=fixture.Factory.Client();Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/health/ready")).StatusCode);Assert.Equal(HttpStatusCode.OK,(await client.GetAsync("/openapi/v1.json")).StatusCode);Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync(Path(Guid.NewGuid())+"/lookups")).StatusCode);
    }
}
