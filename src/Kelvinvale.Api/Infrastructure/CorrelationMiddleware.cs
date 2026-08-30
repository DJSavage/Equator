using System.Security.Claims;
using Serilog.Context;

namespace Kelvinvale.Api.Infrastructure;

/// <summary>
/// Gives every request a correlation id (from <c>X-Correlation-Id</c> or freshly minted), echoes it
/// on the response, and pushes it plus the caller identity into the Serilog <see cref="LogContext"/>
/// so every log line for the request carries who did it and which request it was. Runs after
/// authentication so the caller claims are populated.
/// </summary>
public sealed class CorrelationMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var provided)
                            && !string.IsNullOrWhiteSpace(provided)
            ? provided.ToString()
            : Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        using (LogContext.PushProperty("CallerId", context.User.FindFirstValue(ClaimTypes.NameIdentifier)))
        using (LogContext.PushProperty("CallerRole", context.User.FindFirstValue(ClaimTypes.Role)))
        {
            await next(context);
        }
    }
}
