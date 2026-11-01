namespace SRECommerce.Order.Application;
public interface IOrderSaga
{
 Task Run(Guid tenant,Guid id,string? cartCapability,CancellationToken ct,bool interactive=false);
 Task Recover(CancellationToken ct);
}
