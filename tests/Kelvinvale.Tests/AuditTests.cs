using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kelvinvale.Core.Application;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests;

/// <summary>
/// Requirement 6: compliance can ask who changed what, on whose account, and when. Every mutating
/// action - accepted or refused - leaves an audit entry.
/// </summary>
public sealed class AuditTests(ApiFactory factory) : IntegrationTest(factory)
{
    private Task<JsonElement> AuditFor(Guid customerId) =>
        Factory.AsAdviser(customerId == SeedIds.Customer3 || customerId == SeedIds.Customer4Minor ? SeedIds.Adviser2 : SeedIds.Adviser1)
            .GetFromJsonAsync<JsonElement>($"/api/v1/audit?customerId={customerId}");

    [Fact]
    public async Task Opening_a_product_is_audited_with_the_acting_adviser()
    {
        await Factory.AsAdviser(SeedIds.Adviser1)
            .PostAsJsonAsync($"/api/v1/customers/{SeedIds.Customer2}/products", new { type = "Gia" });

        var entries = await AuditFor(SeedIds.Customer2);
        var opened = entries.EnumerateArray().Single(e => e.GetProperty("action").GetString() == AuditActions.ProductOpened);

        Assert.Equal("Accepted", opened.GetProperty("outcome").GetString());
        Assert.Equal("Adviser", opened.GetProperty("actorRole").GetString());
        Assert.Equal(SeedIds.Adviser1, opened.GetProperty("actorId").GetGuid());
        Assert.True(opened.GetProperty("occurredAtUtc").GetDateTimeOffset() > DateTimeOffset.MinValue);
    }

    [Fact]
    public async Task Placing_an_instruction_is_audited_with_the_acting_customer()
    {
        await Factory.AsCustomer(SeedIds.Customer1).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer1Gia}/instructions",
            new { type = "Subscription", amountPence = 100_000, fundCode = "GLB-EQ-ACC", clientReference = "audit-0001" });

        var entries = await AuditFor(SeedIds.Customer1);
        var placed = entries.EnumerateArray().Single(e => e.GetProperty("action").GetString() == AuditActions.InstructionPlaced);

        Assert.Equal(SeedIds.Customer1, placed.GetProperty("actorId").GetGuid());
        Assert.Equal("Customer", placed.GetProperty("actorRole").GetString());
    }

    [Fact]
    public async Task A_refused_instruction_is_still_audited()
    {
        await Factory.AsCustomer(SeedIds.Customer3).PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer3Isa}/instructions",
            new { type = "Subscription", amountPence = 5_000_000, fundCode = "GLB-EQ-ACC", clientReference = "audit-rej-0001" });

        var entries = await AuditFor(SeedIds.Customer3);
        var refused = entries.EnumerateArray().Single(e => e.GetProperty("action").GetString() == AuditActions.InstructionRefused);

        Assert.Equal("Refused", refused.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Updating_personal_details_records_a_before_and_after_snapshot()
    {
        await Factory.AsCustomer(SeedIds.Customer1).PutAsJsonAsync(
            $"/api/v1/customers/{SeedIds.Customer1}",
            new { firstName = "Grace", lastName = "Adeyemi", email = "grace.adeyemi@example.com", address = "99 New Road, Bath" });

        var entries = await AuditFor(SeedIds.Customer1);
        var updated = entries.EnumerateArray().Single(e => e.GetProperty("action").GetString() == AuditActions.CustomerDetailsUpdated);

        var changes = updated.GetProperty("changesJson").GetString();
        Assert.NotNull(changes);
        Assert.Contains("Okafor", changes); // the "before" surname
        Assert.Contains("Adeyemi", changes); // the "after" surname
    }

    [Fact]
    public async Task Audit_is_scoped_to_the_advisers_own_customers()
    {
        var response = await Factory.AsAdviser(SeedIds.Adviser1)
            .GetAsync($"/api/v1/audit?customerId={SeedIds.Customer3}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
