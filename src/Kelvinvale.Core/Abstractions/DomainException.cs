namespace Kelvinvale.Core.Abstractions;

/// <summary>
/// Thrown when a business rule refuses a request. The API's exception handler turns this into an
/// RFC 9457 <c>application/problem+json</c> response. Defaults to HTTP 422 (well-formed request,
/// rule says no); pass <c>400</c> for a malformed request that never reached a rule.
/// </summary>
public sealed class DomainException : Exception
{
    public const int DefaultStatusCode = 422;

    public DomainException(DomainError error, int statusCode = DefaultStatusCode)
        : base(error.Message)
    {
        Error = error;
        StatusCode = statusCode;
    }

    public DomainError Error { get; }

    public int StatusCode { get; }
}
