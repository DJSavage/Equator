using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Tests.Support;

namespace Kelvinvale.Tests.Products;

/// <summary>
/// An in-flight ISA subscription that crosses 6 April is booked to - and re-checked against - the
/// tax year it settles in (decision D6). These tests advance the clock, so each gets its own host.
/// </summary>
public sealed class SettlementBoundaryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset AfterBoundary = new(2027, 4, 10, 12, 0, 0, TimeSpan.Zero);

    private ApiFactory _factory = null!;

    public async Task InitializeAsync()
    {
        _factory = new ApiFactory();
        await _factory.ResetAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task An_instruction_that_settles_after_6_April_is_booked_to_the_new_tax_year()
    {
        var client = _factory.AsCustomer(SeedIds.Customer1);

        var placed = await client.PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer1Isa}/instructions",
            new { type = "Subscription", amountPence = 500_000, fundCode = "GLB-EQ-ACC", clientReference = "boundary-0001" });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);
        Assert.Equal("2026/27", (await placed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("taxYear").GetString());

        _factory.Clock.SetUtcNow(AfterBoundary);
        await _factory.RunSettlementAsync();

        var settled = await FindAsync(client, "boundary-0001");
        Assert.Equal("Settled", settled.GetProperty("status").GetString());
        Assert.Equal("2027/28", settled.GetProperty("taxYear").GetString());
    }

    [Fact]
    public async Task An_instruction_is_rejected_at_settlement_if_it_no_longer_fits_the_new_year()
    {
        // The new tax year's allowance is already fully used by the time this instruction settles.
        await _factory.WithDbAsync(async db =>
        {
            db.Instructions.Add(new Instruction
            {
                Id = Guid.NewGuid(),
                ProductId = SeedIds.Customer1Isa,
                Type = InstructionType.Subscription,
                AmountPence = 2_000_000,
                FundCode = "GLB-EQ-ACC",
                ClientReference = "boundary-prefill-2027",
                Status = InstructionStatus.Settled,
                CreatedAtUtc = ApiFactory.SeedInstant,
                ValueDate = new DateOnly(2027, 4, 7),
                SettledAtUtc = ApiFactory.SeedInstant,
                CreatedByCallerId = SeedIds.Customer1,
                TaxYear = TaxYear.FromStartYear(2027),
            });
            return await db.SaveChangesAsync();
        });

        var client = _factory.AsCustomer(SeedIds.Customer1);
        var placed = await client.PostAsJsonAsync(
            $"/api/v1/products/{SeedIds.Customer1Isa}/instructions",
            new { type = "Subscription", amountPence = 500_000, fundCode = "GLB-EQ-ACC", clientReference = "boundary-0002" });
        Assert.Equal(HttpStatusCode.Created, placed.StatusCode);

        _factory.Clock.SetUtcNow(AfterBoundary);
        await _factory.RunSettlementAsync();

        var rejected = await FindAsync(client, "boundary-0002");
        Assert.Equal("Rejected", rejected.GetProperty("status").GetString());
        Assert.Equal(ProblemCodes.IsaAllowanceExceeded, rejected.GetProperty("refusalCode").GetString());
        Assert.Equal("2027/28", rejected.GetProperty("taxYear").GetString());
    }

    private static async Task<JsonElement> FindAsync(HttpClient client, string clientReference)
    {
        var list = await client.GetFromJsonAsync<JsonElement>($"/api/v1/products/{SeedIds.Customer1Isa}/instructions");
        return list.EnumerateArray().Single(i => i.GetProperty("clientReference").GetString() == clientReference);
    }
}
