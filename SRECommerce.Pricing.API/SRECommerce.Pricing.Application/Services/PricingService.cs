using System.Security.Cryptography;
using System.Text;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public sealed partial class PricingService(IPricingRepository repository,IPricingAccess access,PriceEngine engine,TimeProvider clock):IPricingService
{
    private DateTimeOffset Now=>clock.GetUtcNow();
    private static string Version(byte[] value)=>Convert.ToBase64String(value);
    private static string Hash<T>(T value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ContractJson.Write(value))));
    private static void Id(Guid value){if(value==Guid.Empty)throw new BusinessFault("InvalidPricingId",400);}
    private static void Page(int value){if(value is <1 or >10000)throw new BusinessFault("InvalidPricingPage",400);}
    private Task<PricingSettings> Config(CancellationToken ct)=>ConfigCore(ct);
    private async Task<PricingSettings> ConfigCore(CancellationToken ct)=>await repository.Settings(ct)??throw new BusinessFault("PricingNotConfigured");
    private static ProductPriceView View(ProductPrice p)=>new(p.Id,p.PriceListId,p.ProductId,p.VariantId,p.Sku,p.IsBundle,p.TaxPolicyId,p.MinimumQuantity,p.Mrp,p.SellingPrice,p.CostPrice,p.Active,Version(p.Version));
    private static object View(TaxPolicy t)=>new{t.Id,t.Code,t.Name,t.Rate,t.Inclusive,t.Active,Version=Version(t.Version)};
    private static object View(CustomerPriceGroup g)=>new{g.Id,g.Code,g.Name,g.Active,Version=Version(g.Version)};
    private static object View(PriceList l)=>new{l.Id,l.Code,l.Name,l.Currency,l.Audience,l.CustomerGroupId,l.Priority,l.StartsAt,l.EndsAt,l.Active,Version=Version(l.Version)};
    private static object View(Domain.Promotion p)=>new{p.Id,p.Code,p.Name,p.Kind,p.Value,p.BuyQuantity,p.GetQuantity,p.MinimumSpend,p.MaximumDiscount,VariantIds=ContractJson.Read<Guid[]>(p.VariantIds),p.CustomerGroupId,p.B2BOnly,p.Automatic,p.Priority,p.StartsAt,p.EndsAt,p.Active,Version=Version(p.Version)};
    private static object View(Domain.Coupon c)=>new{c.Id,c.Code,c.PromotionId,c.GlobalLimit,c.PerCustomerLimit,c.Active,Version=Version(c.Version)};
    private static object View(PricingSettings s)=>new{s.Currency,s.DecimalPlaces,Version=Version(s.Version)};
    private static RedemptionView View(CouponRedemption r)=>new(r.Id,r.QuoteId,r.CouponId,r.Status,r.ExpiresAt,Version(r.Version));
    public async Task<object> Lookups(Guid tenant,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);var settings=await repository.Settings(ct);var metadata=await access.Tenant(tenant,ct);
        return new{Currency=metadata.Currency,Settings=settings is null?null:View(settings),Taxes=(await repository.Taxes(ct)).Select(View),Groups=(await repository.Groups(ct)).Select(View),Lists=(await repository.Lists(ct)).Select(View),Promotions=(await repository.Promotions(ct)).Select(View),Coupons=(await repository.Coupons(ct)).Select(View)};
    }
    public async Task<object> Settings(Guid tenant,SettingsRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Owner,ct);var metadata=await access.Tenant(tenant,ct);if(PricingRules.Currency(request.Currency)!=metadata.Currency)throw new BusinessFault("TenantCurrencyMismatch");PricingRules.Precision(request.DecimalPlaces);
        return await repository.Transaction(async token=>
        {
            var value=await repository.Settings(token);
            if(value is null){value=new PricingSettings{TenantId=tenant,Id=tenant};repository.Add(value);}
            else{repository.Version(value,request.Version);if((value.Currency!=request.Currency||value.DecimalPlaces!=request.DecimalPlaces)&&(await repository.Lists(token)).Any(l=>l.Active))throw new BusinessFault("ArchivePriceListsBeforeCurrencyChange");}
            value.Currency=request.Currency;value.DecimalPlaces=request.DecimalPlaces;await repository.Commit("pricing.settings.saved",tenant,new{value.Currency,value.DecimalPlaces},token);return View(value);
        },ct);
    }
    public async Task<object> Tax(Guid tenant,Guid? id,TaxRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);PricingRules.Tax(request.Rate);
        return await repository.Transaction(async token=>
        {
            var value=id is null?new TaxPolicy{TenantId=tenant}:(await repository.Taxes(token)).SingleOrDefault(t=>t.Id==id)??throw new BusinessFault("TaxPolicyNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            value.Code=PricingRules.Code(request.Code);value.Name=PricingRules.Text(request.Name);value.Rate=request.Rate;value.Inclusive=request.Inclusive;value.Active=request.Active;
            await repository.Commit("pricing.tax.saved",value.Id,new{TaxPolicyId=value.Id,value.Rate,value.Inclusive,value.Active},token);return View(value);
        },ct);
    }
    public async Task<object> Group(Guid tenant,Guid? id,GroupRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);
        return await repository.Transaction(async token=>
        {
            var value=id is null?new CustomerPriceGroup{TenantId=tenant}:(await repository.Groups(token)).SingleOrDefault(g=>g.Id==id)??throw new BusinessFault("PriceGroupNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);value.Code=PricingRules.Code(request.Code);value.Name=PricingRules.Text(request.Name);value.Active=request.Active;
            await repository.Commit("pricing.group.saved",value.Id,null,token);return View(value);
        },ct);
    }
    public async Task<object> AssignGroup(Guid tenant,Guid user,GroupMemberRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Owner,ct);Id(user);await access.CustomerMember(tenant,user,ct);
        return await repository.Transaction(async token=>
        {
            if(!(await repository.Groups(token)).Any(g=>g.Id==request.GroupId&&g.Active))throw new BusinessFault("PriceGroupUnavailable");
            var value=await repository.GroupMember(user,token);
            if(value is null){value=new CustomerGroupMember{TenantId=tenant,UserId=user};repository.Add(value);}else repository.Version(value,request.Version);
            value.GroupId=request.GroupId;value.Active=request.Active;await repository.Commit("pricing.group.member.saved",user,null,token);return new{value.UserId,value.GroupId,value.Active,Version=Version(value.Version)};
        },ct);
    }
    public async Task<object> GroupMember(Guid tenant,Guid user,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Owner,ct);await access.CustomerMember(tenant,user,ct);var value=await repository.GroupMember(user,ct);
        return new{UserId=user,GroupId=value?.GroupId,Active=value?.Active??false,Version=value is null?null:Version(value.Version)};
    }
    public async Task<object> List(Guid tenant,Guid? id,PriceListRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);PricingRules.Window(request.StartsAt,request.EndsAt);if(!Enum.IsDefined(request.Audience)||request.Priority is <-1000 or >1000||request.Audience==PriceAudience.CustomerGroup!=(request.CustomerGroupId is not null))throw new BusinessFault("InvalidPriceList",400);
        return await repository.Transaction(async token=>
        {
            var settings=await Config(token);if(request.Currency!=settings.Currency)throw new BusinessFault("PriceListCurrencyMismatch");
            if(request.CustomerGroupId is {} group&&!(await repository.Groups(token)).Any(g=>g.Id==group&&g.Active))throw new BusinessFault("PriceGroupUnavailable");
            var value=id is null?new PriceList{TenantId=tenant}:(await repository.Lists(token)).SingleOrDefault(l=>l.Id==id)??throw new BusinessFault("PriceListNotFound",404);
            if(id is null)repository.Add(value);else repository.Version(value,request.Version);
            value.Code=PricingRules.Code(request.Code);value.Name=PricingRules.Text(request.Name);value.Currency=PricingRules.Currency(request.Currency);value.Audience=request.Audience;value.CustomerGroupId=request.CustomerGroupId;value.Priority=request.Priority;value.StartsAt=request.StartsAt;value.EndsAt=request.EndsAt;value.Active=request.Active;
            await repository.Commit("pricing.list.saved",value.Id,new{PriceListId=value.Id,value.Active},token);return View(value);
        },ct);
    }
    public async Task<ProductPriceView> Price(Guid tenant,Guid? id,PriceRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);ValidatePrice(request);
        var source=await access.CatalogItem(tenant,request.ProductId,request.VariantId,ct);
        return await repository.Transaction(async token=>
        {
            var settings=await Config(token);if(!(await repository.Lists(token)).Any(l=>l.Id==request.PriceListId&&l.Currency==settings.Currency))throw new BusinessFault("PriceListUnavailable");
            if(!(await repository.Taxes(token)).Any(t=>t.Id==request.TaxPolicyId&&t.Active))throw new BusinessFault("TaxPolicyUnavailable");
            var value=id is null?new ProductPrice{TenantId=tenant,ProductId=source.ProductId,VariantId=source.VariantId}:await repository.Price(id.Value,token)??throw new BusinessFault("PriceNotFound",404);
            var before=id is null?"null":ContractJson.Write(View(value));if(id is null)repository.Add(value);else{repository.Version(value,request.Version);if(value.ProductId!=source.ProductId||value.VariantId!=source.VariantId)throw new BusinessFault("PriceSkuIdentityImmutable");}
            value.PriceListId=request.PriceListId;value.TaxPolicyId=request.TaxPolicyId;value.Sku=source.Sku;value.IsBundle=source.IsBundle;value.MinimumQuantity=request.MinimumQuantity;value.Mrp=request.Mrp;value.SellingPrice=request.SellingPrice;value.CostPrice=request.CostPrice;value.Active=request.Active;
            await repository.Commit("pricing.price.saved",value.Id,new{PriceId=value.Id,value.VariantId,value.Active},token);
            var result=View(value);repository.Add(new PriceHistory{TenantId=tenant,PriceId=value.Id,Before=before,After=ContractJson.Write(result),ChangedAt=Now});await repository.Commit("pricing.price.history",value.Id,null,token);return result;
        },ct);
    }
    public async Task<PricePage<ProductPriceView>> Prices(Guid tenant,int page,Guid? list,string? search,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Manager,ct);Page(page);if(search?.Length>100)throw new BusinessFault("InvalidPriceSearch",400);var values=await repository.PricePage(page,list,search,ct);return new(values.Items.Select(View).ToArray(),page,values.PageSize,values.Total);}
    public async Task<object> History(Guid tenant,Guid price,int page,CancellationToken ct)
    {await access.Authorize(tenant,ActorPermission.Manager,ct);Page(page);return await repository.History(price,page,ct);}
}
