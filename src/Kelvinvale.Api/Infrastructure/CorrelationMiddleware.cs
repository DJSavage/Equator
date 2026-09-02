using System.Security.Claims;
using Serilog.Context;

namespace Kelvinvale.Api.Infrastructure;

/// <summary>
/// Gives every request a correlation id (from <c>X-Correlation-Id</c> or freshly minted), echoes it
/// on the response, and pushes it plus the caller identity into the Serilog <see cref="LogContext"/>
/// so every log line for the request carries who did it and which request it was. Runs after
/// authentication so the caller claims are populated.
/// Used in:
/// 1. Structured logging, tracing, and debugging. The correlation id is a stable identifier for a request
/// 2. The audit trail from ApiControllerBase.Actor
/// ActorContext flows into every handler, and AuditEvents.From(actor, …) copies actor.CorrelationId onto the AuditEntry row.
/// So each row in the audit table records the correlation id of the request that produced it —
/// you can join a log line to the exact audit entry it caused, and vice versa.
/// 3. The response header, so clients can log the correlation id for support and debugging./// 
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
