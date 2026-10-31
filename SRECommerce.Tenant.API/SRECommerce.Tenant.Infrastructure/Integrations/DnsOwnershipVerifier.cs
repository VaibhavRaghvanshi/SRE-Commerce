using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DnsClient;
using Microsoft.AspNetCore.Http;
using SRECommerce.Tenant.Application;
using SRECommerce.Tenant.Domain;

namespace SRECommerce.Tenant.Infrastructure;
public sealed class DnsOwnershipVerifier : IDomainOwnershipVerifier
{
    private readonly LookupClient lookup=new(new LookupClientOptions {Timeout=TimeSpan.FromSeconds(2),Retries=1,UseCache=false});
    public async Task<IReadOnlyCollection<string>> GetTxtAsync(string host,CancellationToken ct)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(5));
        try {var answer=await lookup.QueryAsync(host,QueryType.TXT,QueryClass.IN,deadline.Token);return answer.Answers.TxtRecords().Select(r=>string.Concat(r.Text)).ToArray();}
        catch(DnsResponseException) {throw new TenantRuleException("DomainDnsUnavailable",503);}
        catch(OperationCanceledException) when(!ct.IsCancellationRequested) {throw new TenantRuleException("DomainDnsUnavailable",503);}
    }
}
