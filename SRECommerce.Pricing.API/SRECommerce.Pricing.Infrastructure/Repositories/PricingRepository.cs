using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Pricing.Application;
using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Infrastructure;
public sealed class PricingRepository(PricingDbContext db,ModuleScope scope,IHttpContextAccessor contexts,ServiceSpec service):IPricingRepository
{
    public async Task<T> Transaction<T>(Func<CancellationToken,Task<T>> action,CancellationToken ct)
    {await using var transaction=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);var result=await action(ct);await transaction.CommitAsync(ct);return result;}
    public Task<PricingSettings?> Settings(CancellationToken ct)=>db.Settings.SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyCollection<TaxPolicy>> Taxes(CancellationToken ct)=>await db.Taxes.OrderBy(t=>t.Code).ToListAsync(ct);
    public async Task<IReadOnlyCollection<PriceList>> Lists(CancellationToken ct)=>await db.Lists.OrderBy(l=>l.Code).ToListAsync(ct);
    public async Task<IReadOnlyCollection<CustomerPriceGroup>> Groups(CancellationToken ct)=>await db.Groups.OrderBy(g=>g.Code).ToListAsync(ct);
    public Task<CustomerGroupMember?> GroupMember(Guid user,CancellationToken ct)=>db.Members.SingleOrDefaultAsync(m=>m.UserId==user,ct);
    public async Task<IReadOnlyCollection<ProductPrice>> Prices(Guid[]? variants,Guid? list,CancellationToken ct)
    {var query=db.Prices.AsQueryable();if(variants is not null)query=query.Where(p=>variants.Contains(p.VariantId));if(list is {} id)query=query.Where(p=>p.PriceListId==id);return await query.OrderBy(p=>p.Id).ToListAsync(ct);}
    public async Task<PricePage<ProductPrice>> PricePage(int page,Guid? list,string? search,CancellationToken ct)
    {var query=db.Prices.AsQueryable();if(list is {} id)query=query.Where(p=>p.PriceListId==id);if(!string.IsNullOrWhiteSpace(search))query=query.Where(p=>p.Sku.Contains(search));var total=await query.CountAsync(ct);return new(await query.OrderBy(p=>p.Sku).ThenBy(p=>p.Id).Skip((page-1)*50).Take(50).ToListAsync(ct),page,50,total);}
    public Task<ProductPrice?> Price(Guid id,CancellationToken ct)=>db.Prices.SingleOrDefaultAsync(p=>p.Id==id,ct);
    public Task<PricingImport?> Import(Guid id,CancellationToken ct)=>db.Imports.SingleOrDefaultAsync(p=>p.Id==id,ct);
    public async Task<IReadOnlyCollection<Promotion>> Promotions(CancellationToken ct)=>await db.Promotions.OrderBy(p=>p.Code).ToListAsync(ct);
    public async Task<IReadOnlyCollection<Coupon>> Coupons(CancellationToken ct)=>await db.Coupons.OrderBy(c=>c.Code).ToListAsync(ct);
    public Task<PricingQuote?> Quote(Guid id,CancellationToken ct)=>db.Quotes.SingleOrDefaultAsync(q=>q.Id==id,ct);
    public Task<CouponRedemption?> Redemption(Guid id,CancellationToken ct)=>db.Redemptions.SingleOrDefaultAsync(r=>r.Id==id,ct);
    public Task<int> CouponUsage(Guid coupon,Guid? user,bool perUser,DateTimeOffset now,CancellationToken ct)=>db.Redemptions.CountAsync(r=>r.CouponId==coupon&&(!perUser||r.UserId==user)&&(r.Status==RedemptionStatus.Redeemed||r.Status==RedemptionStatus.Reserved&&r.ExpiresAt>now),ct);
    public async Task<IReadOnlyCollection<PriceHistory>> History(Guid price,int page,CancellationToken ct)=>await db.History.AsNoTracking().Where(h=>h.PriceId==price).OrderByDescending(h=>h.ChangedAt).ThenBy(h=>h.Id).Skip((page-1)*50).Take(50).ToListAsync(ct);
    public void Add<T>(T entity)where T:class=>db.Add(entity);
    public void Version(BuildingBlocks.Kernel.IVersionedRecord entity,string? expected)=>db.ExpectedVersion(entity,expected??"");
    public async Task Commit(string action,Guid resource,object? integrationEvent,CancellationToken ct)
    {db.Record(scope,contexts,action,resource.ToString());if(integrationEvent is not null)db.Emit(scope,service,contexts,action+".v1",integrationEvent);await db.SaveChangesAsync(ct);}
}
