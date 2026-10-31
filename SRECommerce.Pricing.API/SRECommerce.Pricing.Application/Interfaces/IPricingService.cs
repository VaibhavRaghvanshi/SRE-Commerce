namespace SRECommerce.Pricing.Application;
public interface IPricingService
{
    Task<Guid[]> Import(Guid tenant,PriceImportRequest request,CancellationToken ct);
    Task<object> Lookups(Guid tenant,CancellationToken ct);
    Task<object> Settings(Guid tenant,SettingsRequest request,CancellationToken ct);
    Task<object> Tax(Guid tenant,Guid? id,TaxRequest request,CancellationToken ct);
    Task<object> Group(Guid tenant,Guid? id,GroupRequest request,CancellationToken ct);
    Task<object> AssignGroup(Guid tenant,Guid user,GroupMemberRequest request,CancellationToken ct);
    Task<object> GroupMember(Guid tenant,Guid user,CancellationToken ct);
    Task<object> List(Guid tenant,Guid? id,PriceListRequest request,CancellationToken ct);
    Task<ProductPriceView> Price(Guid tenant,Guid? id,PriceRequest request,CancellationToken ct);
    Task<PricePage<ProductPriceView>> Prices(Guid tenant,int page,Guid? list,string? search,CancellationToken ct);
    Task<object> History(Guid tenant,Guid price,int page,CancellationToken ct);
    Task<object> Promotion(Guid tenant,Guid? id,PromotionRequest request,CancellationToken ct);
    Task<object> Coupon(Guid tenant,Guid? id,CouponRequest request,CancellationToken ct);
    Task<QuoteView> Quote(Guid tenant,QuoteRequest request,bool publicRead,CancellationToken ct);
    Task<QuoteView> QuoteById(Guid tenant,Guid id,bool machine,CancellationToken ct);
    Task<RedemptionView> Reserve(Guid tenant,RedemptionRequest request,CancellationToken ct);
    Task<RedemptionView> Finish(Guid tenant,Guid id,bool redeem,string version,CancellationToken ct);
}
