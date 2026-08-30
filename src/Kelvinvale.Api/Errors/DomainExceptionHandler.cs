using Kelvinvale.Core.Abstractions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Kelvinvale.Api.Errors;

/// <summary>
/// Turns a <see cref="DomainException"/> - a request a business rule refused - into an RFC 9457
/// <c>application/problem+json</c> response with a stable <c>type</c> URI and machine <c>code</c>.
/// Anything else falls through to the framework's default handling.
/// </summary>
public sealed class DomainExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<DomainExceptionHandler> logger) : IExceptionHandler
{
    private const string ProblemTypeBase = "https://kelvinvale.example/problems/";

    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DomainException domain)
        {
            return false;
        }

        logger.LogWarning(
            "Business rule {Code} refused a request to {Path} ({Status}).",
            domain.Error.Code, httpContext.Request.Path, domain.StatusCode);

        httpContext.Response.StatusCode = domain.StatusCode;

        var problem = new ProblemDetails
        {
            Status = domain.StatusCode,
            Title = domain.StatusCode == 400 ? "The request could not be processed." : "The request was refused by a business rule.",
            Detail = domain.Error.Message,
            Type = ProblemTypeBase + domain.Error.Code,
        };
        problem.Extensions["code"] = domain.Error.Code;

        if (domain.Error.Extensions is not null)
        {
            foreach (var (key, value) in domain.Error.Extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
