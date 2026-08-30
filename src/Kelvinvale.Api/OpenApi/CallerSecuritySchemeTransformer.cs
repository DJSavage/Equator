using Kelvinvale.Api.Auth;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Kelvinvale.Api.OpenApi;

/// <summary>
/// Declares the stub identity headers (<c>X-Caller-Id</c> / <c>X-Caller-Role</c>) as API-key
/// security schemes so Swagger UI and Scalar show an "Authorize" box for them and attach them to
/// every "try it out" request. In production these would be replaced by a bearer scheme.
/// </summary>
internal sealed class CallerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    private const string CallerIdScheme = "CallerId";
    private const string CallerRoleScheme = "CallerRole";

    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();

        document.Components.SecuritySchemes[CallerIdScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = StubAuthenticationHandler.CallerIdHeader,
            Description = "The caller's id (GUID). Use a seeded adviser or customer id.",
        };

        document.Components.SecuritySchemes[CallerRoleScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = StubAuthenticationHandler.CallerRoleHeader,
            Description = "The caller's role: Adviser or Customer.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(CallerIdScheme, document)] = [],
            [new OpenApiSecuritySchemeReference(CallerRoleScheme, document)] = [],
        });

        return Task.CompletedTask;
    }
}
