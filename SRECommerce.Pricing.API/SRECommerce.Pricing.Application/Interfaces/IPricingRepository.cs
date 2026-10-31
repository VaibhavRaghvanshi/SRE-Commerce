using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public interface IPricingRepository
{
    Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct);
    Task<PricingSettings?> Settings(CancellationToken ct);
    Task<IReadOnlyCollection<TaxPolicy>> Taxes(CancellationToken ct);
    Task<IReadOnlyCollection<PriceList>> Lists(CancellationToken ct);
    Task<IReadOnlyCollection<CustomerPriceGroup>> Groups(CancellationToken ct);
    Task<CustomerGroupMember?> GroupMember(Guid user,CancellationToken ct);
    Task<IReadOnlyCollection<ProductPrice>> Prices(Guid[]? variants,Guid? list,CancellationToken ct);
    Task<PricePage<ProductPrice>> PricePage(int page,Guid? list,string? search,CancellationToken ct);
    Task<ProductPrice?> Price(Guid id,CancellationToken ct);
    Task<IReadOnlyCollection<Promotion>> Promotions(CancellationToken ct);
    Task<IReadOnlyCollection<Coupon>> Coupons(CancellationToken ct);
    Task<PricingQuote?> Quote(Guid id,CancellationToken ct);
    Task<CouponRedemption?> Redemption(Guid id,CancellationToken ct);
    Task<int> CouponUsage(Guid coupon,Guid? user,bool perUser,DateTimeOffset now,CancellationToken ct);
    Task<IReadOnlyCollection<PriceHistory>> History(Guid price,int page,CancellationToken ct);
    void Add<T>(T entity)where T:class;
    void Version(BuildingBlocks.Kernel.IVersionedRecord entity,string? expected);
    Task Commit(string action,Guid resource,object? integrationEvent,CancellationToken ct);
}
