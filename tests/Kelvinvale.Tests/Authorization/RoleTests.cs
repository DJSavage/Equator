using System.Net;
using System.Net.Http.Json;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests.Authorization;

/// <summary>Role boundaries: customers cannot do adviser things, advisers cannot do customer things - even on records they own.</summary>
public sealed class RoleTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Customer_cannot_create_a_customer()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer1).PostAsJsonAsync(
            "/api/v1/customers",
            new { firstName = "New", lastName = "Person", dateOfBirth = "1990-01-01", email = "n@example.com", address = "1 St" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Customer_cannot_open_a_product_even_on_their_own_record()
    {
        var response = await Factory.AsCustomer(SeedIds.Customer1)
            .PostAsJsonAsync($"/api/v1/customers/{SeedIds.Customer1}/products", new { type = "Gia" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Adviser_cannot_place_an_instruction_even_for_a_customer_they_look_after()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser1).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer1Isa}/instructions",
            new { type = "Subscription", amountPence = 10_000, fundCode = "GLB-EQ-ACC", clientReference = "role-0001" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Adviser_cannot_update_a_customers_personal_details()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser1).PutAsJsonAsync(
            $"/api/v1/customers/{SeedIds.Customer1}",
            new { firstName = "Grace", lastName = "Okafor", email = "grace@example.com", address = "New Address" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
