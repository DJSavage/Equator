using System.Net;
using System.Net.Http.Json;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests.Products;

/// <summary>SIPP rules. Customer 4 is 16; Customer 2 is an adult under the minimum pension age and holds a SIPP.</summary>
public sealed class SippRuleTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task A_SIPP_cannot_be_opened_for_a_customer_under_18()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser2)
            .PostAsJsonAsync($"/api/v1/customers/{SeedIds.Customer4Minor}/products", new { type = "Sipp" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ProblemCodes.SippMinimumAge, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_SIPP_can_be_opened_for_an_adult()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser1)
            .PostAsJsonAsync($"/api/v1/customers/{SeedIds.Customer1}/products", new { type = "Sipp" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_withdrawal_before_the_minimum_pension_age_is_refused()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer2Sipp}/instructions",
            new { type = "Withdrawal", amountPence = 100_000, fundCode = "GLB-EQ-ACC", clientReference = "sipp-wd-0001" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ProblemCodes.SippWithdrawalAge, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_subscription_to_a_SIPP_is_accepted()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer2Sipp}/instructions",
            new { type = "Subscription", amountPence = 100_000, fundCode = "GLB-EQ-ACC", clientReference = "sipp-sub-0001" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
