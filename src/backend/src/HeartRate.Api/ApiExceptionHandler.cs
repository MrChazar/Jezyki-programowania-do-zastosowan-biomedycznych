using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;

namespace HeartRate.Api;

public sealed class ApiExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        if (exception is BadHttpRequestException badRequest)
        {
            context.Response.StatusCode = badRequest.StatusCode;
            await context.Response.WriteAsJsonAsync(new { message = "Niepoprawne dane żądania." }, ct);
            return true;
        }
        if (exception is DbUpdateConcurrencyException)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { message = "Dane zostały zmienione. Ponów żądanie." }, ct);
            return true;
        }
        if (IsDuplicateKey(exception))
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            await context.Response.WriteAsJsonAsync(new { message = "Dane o takim identyfikatorze lub emailu już istnieją." }, ct);
            return true;
        }
        return false;
    }

    private static bool IsDuplicateKey(Exception exception) =>
        (exception is MongoException && exception.Message.Contains("E11000", StringComparison.Ordinal))
        || (exception.InnerException is not null && IsDuplicateKey(exception.InnerException));
}
