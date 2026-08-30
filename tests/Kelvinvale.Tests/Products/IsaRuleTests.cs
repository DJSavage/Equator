using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests.Products;

/// <summary>
/// ISA rules. Seed state: Customer 3's ISA already has £19,500 of a £20,000 allowance subscribed
/// for the current tax year; Customer 1's ISA has £5,000 subscribed.
/// </summary>
public sealed class IsaRuleTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task A_second_ISA_in_the_same_tax_year_is_refused()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser2)
            .PostAsJsonAsync($"/api/v1/customers/{SeedIds.Customer3}/products", new { type = "Isa" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(ProblemCodes.IsaOnePerTaxYear, await response.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_subscription_over_the_remaining_allowance_is_refused_whole()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer3).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer3Isa}/instructions",
            new { type = "Subscription", amountPence = 60_000, fundCode = "GLB-EQ-ACC", clientReference = "isa-over-0001" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = await response.ReadProblemAsync();
        Assert.Equal(ProblemCodes.IsaAllowanceExceeded, problem.GetProperty("code").GetString());
        Assert.Equal(50_000, problem.GetProperty("remainingAllowancePence").GetInt64());
    }

    [Fact]
    public async Task A_subscription_equal_to_the_remaining_allowance_is_accepted()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer3).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer3Isa}/instructions",
            new { type = "Subscription", amountPence = 50_000, fundCode = "GLB-EQ-ACC", clientReference = "isa-fits-0001" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Received", body.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Two_subscriptions_that_individually_fit_but_jointly_breach_are_caught()
    {
        var client = Factory.AsCustomer(SeedIds.Customer3);

        var first = await client.PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer3Isa}/instructions",
            new { type = "Subscription", amountPence = 30_000, fundCode = "GLB-EQ-ACC", clientReference = "isa-joint-0001" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // £300 accepted (pending), £200 headroom left; a second £300 must now be refused.
        var second = await client.PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer3Isa}/instructions",
            new { type = "Subscription", amountPence = 30_000, fundCode = "GLB-EQ-ACC", clientReference = "isa-joint-0002" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, second.StatusCode);
        Assert.Equal(ProblemCodes.IsaAllowanceExceeded, await second.ProblemCodeAsync());
    }

    [Fact]
    public async Task A_withdrawal_is_not_constrained_by_the_subscription_allowance()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer3).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer3Isa}/instructions",
            new { type = "Withdrawal", amountPence = 100_000, fundCode = "GLB-EQ-ACC", clientReference = "isa-wd-0001" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task A_refused_subscription_is_recorded_as_a_rejected_instruction()
    {
        var client = Factory.AsCustomer(SeedIds.Customer3);
        await client.PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer3Isa}/instructions",
            new { type = "Subscription", amountPence = 60_000, fundCode = "GLB-EQ-ACC", clientReference = "isa-rej-0001" });

        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{SeedIds.Customer3Isa}/instructions");
        var rejected = list.EnumerateArray()
            .Single(i => i.GetProperty("clientReference").GetString() == "isa-rej-0001");

        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());
        Assert.Equal(ProblemCodes.IsaAllowanceExceeded, rejected.GetProperty("refusalCode").GetString());
    }
}
