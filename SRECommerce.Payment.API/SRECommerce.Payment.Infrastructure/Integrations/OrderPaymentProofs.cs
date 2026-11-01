using System.Net;
using SRECommerce.BuildingBlocks.Contracts;
using SRECommerce.BuildingBlocks.Infrastructure;
using SRECommerce.Payment.Application;
namespace SRECommerce.Payment.Infrastructure;
public sealed class OrderPaymentProofs(ServiceHttp transport) : IOrderPaymentProofs
{
    public async Task<OrderPaymentProof> Get(Guid tenant, Guid order, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"internal/v1/tenants/{tenant}/orders/{order}/context");
        using var response = await transport.Send("Order", request, ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) throw new BusinessFault("OrderPaymentProofUnavailable", (int)response.StatusCode);
        if (!response.IsSuccessStatusCode) throw new DependencyFault("Order");
        return await RemoteAccess.Read<OrderPaymentProof>(response, "Order", ct) ?? throw new DependencyFault("Order");
    }
}
