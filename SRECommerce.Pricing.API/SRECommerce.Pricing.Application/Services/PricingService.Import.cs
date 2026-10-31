using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.Pricing.Domain;
namespace SRECommerce.Pricing.Application;
public sealed partial class PricingService
{
    public async Task<Guid[]> Import(Guid tenant,PriceImportRequest request,CancellationToken ct)
    {
        await access.Authorize(tenant,ActorPermission.Manager,ct);Id(request.Key);PricingRules.Text(request.Source,80);
        if(request.Prices is null||request.Prices.Length is <1 or >100||request.Prices.Select(p=>(p.PriceListId,p.VariantId,p.MinimumQuantity)).Distinct().Count()!=request.Prices.Length||request.Prices.Any(p=>p.Version is not null))throw new BusinessFault("InvalidPriceImport",400);
        foreach(var row in request.Prices)ValidatePrice(row);
        var hash=Hash(new{Action="price-import",request.Source,request.Prices});
        return await repository.Transaction(async token=>
        {
            var prior=await repository.Import(request.Key,token);if(prior is not null){if(prior.Hash!=hash)throw new BusinessFault("ImportKeyPayloadConflict");return ContractJson.Read<Guid[]>(prior.Result);}
            var catalog=await access.CatalogItems(tenant,request.Prices.Select(p=>p.VariantId).Distinct().ToArray(),token);var settings=await Config(token);var lists=await repository.Lists(token);var taxes=await repository.Taxes(token);
            var existing=await repository.Prices(request.Prices.Select(p=>p.VariantId).Distinct().ToArray(),null,token);var values=new List<ProductPrice>();
            foreach(var row in request.Prices)
            {
                var sku=catalog.SingleOrDefault(s=>s.ProductId==row.ProductId&&s.VariantId==row.VariantId)??throw new BusinessFault("CatalogVariantUnavailable");
                if(!lists.Any(l=>l.Id==row.PriceListId&&l.Currency==settings.Currency))throw new BusinessFault("PriceListUnavailable");
                if(!taxes.Any(t=>t.Id==row.TaxPolicyId&&t.Active))throw new BusinessFault("TaxPolicyUnavailable");
                if(existing.Any(p=>p.PriceListId==row.PriceListId&&p.VariantId==row.VariantId&&p.MinimumQuantity==row.MinimumQuantity))throw new BusinessFault("PriceTierAlreadyExists");
                var value=new ProductPrice{TenantId=tenant,PriceListId=row.PriceListId,ProductId=sku.ProductId,VariantId=sku.VariantId,Sku=sku.Sku,IsBundle=sku.Kind==2,TaxPolicyId=row.TaxPolicyId,MinimumQuantity=row.MinimumQuantity,Mrp=row.Mrp,SellingPrice=row.SellingPrice,CostPrice=row.CostPrice,Active=row.Active};repository.Add(value);values.Add(value);
            }
            var ids=values.Select(p=>p.Id).ToArray();await repository.Commit("pricing.prices.imported",request.Key,new{request.Key,request.Source,PriceIds=ids},token);
            foreach(var value in values)repository.Add(new PriceHistory{TenantId=tenant,PriceId=value.Id,Before="null",After=ContractJson.Write(View(value)),ChangedAt=Now});
            repository.Add(new PricingImport{TenantId=tenant,Id=request.Key,Source=request.Source,Hash=hash,Result=ContractJson.Write(ids),CreatedAt=Now});await repository.Commit("pricing.import.recorded",request.Key,null,token);return ids;
        },ct);
    }
    private static void ValidatePrice(PriceRequest request)
    {
        Id(request.ProductId);Id(request.VariantId);PricingRules.Quantity(request.MinimumQuantity);PricingRules.Money(request.Mrp);PricingRules.Money(request.SellingPrice);if(request.CostPrice is {} cost)PricingRules.Money(cost);if(request.SellingPrice>request.Mrp)throw new BusinessFault("SellingPriceExceedsMrp",400);
    }
}
