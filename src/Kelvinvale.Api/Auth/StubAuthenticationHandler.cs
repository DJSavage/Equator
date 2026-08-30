using System.Security.Claims;
using System.Text.Encodings.Web;
using Kelvinvale.Core.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Kelvinvale.Api.Auth;

public sealed class StubAuthenticationOptions : AuthenticationSchemeOptions;

/// <summary>
/// Stands in for a real identity provider. Reads <c>X-Caller-Id</c> and <c>X-Caller-Role</c> and
/// turns them into the same claims a JWT bearer token would carry, so swapping to a real IdP is a
/// registration change and nothing downstream moves. Missing headers -&gt; no result (the fallback
/// policy then challenges); malformed headers -&gt; explicit failure.
/// </summary>
public sealed class StubAuthenticationHandler(
    IOptionsMonitor<StubAuthenticationOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<StubAuthenticationOptions>(options, logger, encoder)
{
    public const string SchemeName = "Stub";
    public const string CallerIdHeader = "X-Caller-Id";
    public const string CallerRoleHeader = "X-Caller-Role";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(CallerIdHeader, out var idValues) ||
            !Request.Headers.TryGetValue(CallerRoleHeader, out var roleValues))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (!Guid.TryParse(idValues.ToString(), out var callerId))
        {
            return Task.FromResult(AuthenticateResult.Fail($"Malformed {CallerIdHeader} header."));
        }

        var role = roleValues.ToString();
        if (role != Roles.Adviser && role != Roles.Customer)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Unknown {CallerRoleHeader} '{role}'."));
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, callerId.ToString()), new Claim(ClaimTypes.Role, role)],
            SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
