using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kelvinvale.Api.Auth;
using Kelvinvale.Core.Auth;

namespace Kelvinvale.Tests.Support;

public static class TestClient
{
    /// <summary>A client that presents the given caller id and role on every request.</summary>
    public static HttpClient As(this ApiFactory factory, Guid callerId, string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(StubAuthenticationHandler.CallerIdHeader, callerId.ToString());
        client.DefaultRequestHeaders.Add(StubAuthenticationHandler.CallerRoleHeader, role);
        return client;
    }

    public static HttpClient AsAdviser(this ApiFactory factory, Guid callerId) => factory.As(callerId, Roles.Adviser);

    public static HttpClient AsCustomer(this ApiFactory factory, Guid callerId) => factory.As(callerId, Roles.Customer);

    public static HttpClient Anonymous(this ApiFactory factory) => factory.CreateClient();

    /// <summary>Reads the problem body once into a buffered <see cref="JsonElement"/>.</summary>
    public static async Task<JsonElement> ReadProblemAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    /// <summary>Reads the <c>code</c> member from an RFC 9457 problem response.</summary>
    public static async Task<string?> ProblemCodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.ReadProblemAsync();
        return problem.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    public static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, MediaTypeHeaderValue.Parse("application/json").MediaType);
}
