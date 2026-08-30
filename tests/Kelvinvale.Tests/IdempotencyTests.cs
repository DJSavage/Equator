using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests;

/// <summary>A replayed client reference yields the same outcome and never a second instruction.</summary>
public sealed class IdempotencyTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Replaying_a_client_reference_returns_the_original_instruction()
    {
        var client = Factory.AsCustomer(SeedIds.Customer1);
        var body = new { type = "Subscription", amountPence = 100_000, fundCode = "GLB-EQ-ACC", clientReference = "idem-0001" };

        var first = await client.PostAsJsonAsync($"/api/v1/products/{SeedIds.Customer1Gia}/instructions", body);
        var second = await client.PostAsJsonAsync($"/api/v1/products/{SeedIds.Customer1Gia}/instructions", body);

        var firstId = (await first.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var secondId = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        Assert.Equal(firstId, secondId);

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{SeedIds.Customer1Gia}/instructions");
        var matching = list.EnumerateArray().Count(i => i.GetProperty("clientReference").GetString() == "idem-0001");
        Assert.Equal(1, matching);
    }

    [Fact]
    public async Task Replaying_a_previously_refused_client_reference_is_refused_again()
    {
        var client = Factory.AsCustomer(SeedIds.Customer3);
        var body = new { type = "Subscription", amountPence = 5_000_000, fundCode = "GLB-EQ-ACC", clientReference = "idem-rej-0001" };

        var first = await client.PostAsJsonAsync($"/api/v1/products/{SeedIds.Customer3Isa}/instructions", body);
        var second = await client.PostAsJsonAsync($"/api/v1/products/{SeedIds.Customer3Isa}/instructions", body);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, first.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
    }
}
