using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace SRECommerce.Identity.API;

public sealed class IdentityExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var sql = exception as SqlException ?? exception.InnerException as SqlException;
        var status = exception is DbUpdateConcurrencyException || sql?.Number is 2601 or 2627 or 547 ? 409
            : sql is not null ? 503 : 500;
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status=status, Title=status==409 ? "The account state changed; reload and retry." : status==503 ? "Identity storage is temporarily unavailable." : "The request could not be completed."
        }, cancellationToken);
        return true;
    }
}
