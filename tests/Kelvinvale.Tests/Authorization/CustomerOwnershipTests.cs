using System.Net;
using System.Net.Http.Json;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests.Authorization;

/// <summary>
/// The negative authorisation paths: pushing at the API from the wrong account. Customer 1 and
/// Customer 2 belong to Adviser 1; Customer 3 and the minor Customer 4 belong to Adviser 2.
/// </summary>
public sealed class CustomerOwnershipTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly object WellFormedInstruction = new
    {
        type = "Subscription",
        amountPence = 10_000,
        fundCode = "GLB-EQ-ACC",
        clientReference = "ownership-probe-0001",
    };

    [Fact]
    public async Task Customer_cannot_read_another_customers_record()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2)
            .GetAsync($"/api/v1/customers/{SeedIds.Customer1}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_list_another_customers_products()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2)
            .GetAsync($"/api/v1/customers/{SeedIds.Customer1}/products");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_product()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2)
            .GetAsync($"/api/v1/products/{SeedIds.Customer1Isa}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_holdings()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2)
            .GetAsync($"/api/v1/products/{SeedIds.Customer1Isa}/holdings");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_read_another_customers_instructions()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2)
            .GetAsync($"/api/v1/products/{SeedIds.Customer1Isa}/instructions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_place_an_instruction_against_another_customers_isa()
    {
        // The headline: no customer should ever move another customer's money.
        var response = await Factory.AsCustomer(SeedIds.Customer2)
            .PostAsJsonAsync($"/api/v1/products/{SeedIds.Customer1Isa}/instructions", WellFormedInstruction);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_update_another_customers_details()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer2).PutAsJsonAsync(
            $"/api/v1/customers/{SeedIds.Customer1}",
            new { firstName = "Mallory", lastName = "Nobody", email = "m@example.com", address = "Nowhere" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adviser_cannot_reach_a_customer_they_do_not_look_after()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser1)
            .GetAsync($"/api/v1/customers/{SeedIds.Customer3}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Adviser_cannot_open_a_product_for_someone_elses_customer()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser1)
            .PostAsJsonAsync($"/api/v1/customers/{SeedIds.Customer3}/products", new { type = "Gia" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        var response = await Factory.Anonymous()
            .GetAsync($"/api/v1/customers/{SeedIds.Customer1}/products");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_caller_id_is_rejected()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Caller-Id", "not-a-guid");
        client.DefaultRequestHeaders.Add("X-Caller-Role", "Customer");

        var response = await client.GetAsync($"/api/v1/customers/{SeedIds.Customer1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_role_is_rejected()
    {
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Caller-Id", SeedIds.Customer1.ToString());
        client.DefaultRequestHeaders.Add("X-Caller-Role", "Superuser");

        var response = await client.GetAsync($"/api/v1/customers/{SeedIds.Customer1}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
