using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests.Products;

public sealed class GiaTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task A_GIA_can_be_opened_with_no_eligibility_test()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser1)
            .PostAsJsonAsync($"/api/v1/customers/{SeedIds.Customer2}/products", new { type = "Gia" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Every_GIA_instruction_is_flagged_reportable()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer1).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer1Gia}/instructions",
            new { type = "Subscription", amountPence = 250_000, fundCode = "GLB-EQ-ACC", clientReference = "gia-0001" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("reportable").GetBoolean());
    }
}
