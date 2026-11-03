using System.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Manufacturing.Application;
using SRECommerce.Manufacturing.Domain;

namespace SRECommerce.Manufacturing.Infrastructure;

public sealed class ManufacturingStore(ManufacturingDbContext db, ModuleScope scope, IHttpContextAccessor contexts) : IManufacturingStore
{
    public async Task<T> Transaction<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var result = await action(ct); await tx.CommitAsync(ct); return result;
    }
    public Task<BillOfMaterials?> Bom(Guid id, CancellationToken ct) => db.Boms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<BillOfMaterials[]> Boms(int page, CancellationToken ct) => db.Boms.AsNoTracking().OrderBy(x => x.Code).ThenByDescending(x => x.Revision).Skip((page - 1) * 50).Take(50).ToArrayAsync(ct);
    public async Task<int> Revision(string code, CancellationToken ct) => await db.Boms.Where(x => x.Code == code).MaxAsync(x => (int?)x.Revision, ct) ?? 0;
    public Task<ProductionOrder?> Order(Guid id, CancellationToken ct) => db.Orders.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<ProductionOrder[]> Orders(int page, CancellationToken ct) => db.Orders.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50).ToArrayAsync(ct);
    public Task<ProductionHistory[]> History(Guid id, CancellationToken ct) => db.History.AsNoTracking().Where(x => x.OrderId == id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArrayAsync(ct);
    public void Add<T>(T row) where T : class => db.Add(row);
    public void Version(ProductionOrder row, string expected) => db.ExpectedVersion(row, expected);
    public async Task Save(string action, Guid resource, CancellationToken ct)
    { db.Record(scope, contexts, action, resource.ToString()); await db.SaveChangesAsync(ct); }
}
