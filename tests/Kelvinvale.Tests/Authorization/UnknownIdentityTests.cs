using System.Net;
using System.Net.Http.Json;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests.Authorization;

/// <summary>
/// The stub authentication handler trusts headers, not the database: a caller can present any
/// well-formed id alongside a role claim the id does not actually hold. Role and ownership checks
/// pass on the claim alone, so the database's own foreign key is the backstop - and it must fail
/// cleanly, not as an unhandled exception.
/// </summary>
public sealed class UnknownIdentityTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Creating_a_customer_as_a_caller_with_no_matching_adviser_row_is_refused_cleanly()
    {
        // SeedIds.Customer1 is a real, seeded id - just not an adviser's. The "IsAdviser" policy
        // only checks the role claim, so this reaches the handler and hits the AdviserId foreign key.
        var response = await Factory.AsAdviser(SeedIds.Customer1).PostAsJsonAsync(
            "/api/v1/customers",
            new { firstName = "New", lastName = "Person", dateOfBirth = "1990-01-01", email = "n@example.com", address = "1 St" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ProblemCodes.AdviserUnknown, await response.ProblemCodeAsync());
    }
}
