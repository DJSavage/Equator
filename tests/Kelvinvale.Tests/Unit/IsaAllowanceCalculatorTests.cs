using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Products;

namespace Kelvinvale.Tests.Unit;

public sealed class IsaAllowanceCalculatorTests
{
    private static readonly TaxYear ThisYear = TaxYear.FromStartYear(2026);
    private static readonly TaxYear LastYear = TaxYear.FromStartYear(2025);

    private static Instruction Sub(long pence, InstructionStatus status, TaxYear taxYear, InstructionType type = InstructionType.Subscription) => new()
    {
        Id = Guid.NewGuid(),
        FundCode = "GLB-EQ-ACC",
        ClientReference = Guid.NewGuid().ToString(),
        AmountPence = pence,
        Status = status,
        TaxYear = taxYear,
        Type = type,
    };

    [Fact]
    public void Counts_received_and_settled_subscriptions_in_the_year()
    {
        var instructions = new[]
        {
            Sub(500_000, InstructionStatus.Settled, ThisYear),
            Sub(300_000, InstructionStatus.Received, ThisYear),
        };

        Assert.Equal(800_000, IsaAllowanceCalculator.SubscribedPence(instructions, ThisYear));
    }

    [Fact]
    public void Ignores_other_tax_years_withdrawals_and_rejected_instructions()
    {
        var instructions = new[]
        {
            Sub(999_999, InstructionStatus.Settled, LastYear),
            Sub(999_999, InstructionStatus.Rejected, ThisYear),
            Sub(999_999, InstructionStatus.Settled, ThisYear, InstructionType.Withdrawal),
        };

        Assert.Equal(0, IsaAllowanceCalculator.SubscribedPence(instructions, ThisYear));
    }

    [Fact]
    public void Can_exclude_a_specific_instruction()
    {
        var mine = Sub(400_000, InstructionStatus.Received, ThisYear);
        var instructions = new[] { mine, Sub(600_000, InstructionStatus.Settled, ThisYear) };

        Assert.Equal(600_000, IsaAllowanceCalculator.SubscribedPence(instructions, ThisYear, mine.Id));
    }

    [Fact]
    public void Remaining_is_the_allowance_less_what_is_subscribed()
    {
        var instructions = new[] { Sub(1_950_000, InstructionStatus.Settled, ThisYear) };

        Assert.Equal(50_000, IsaAllowanceCalculator.RemainingPence(instructions, ThisYear));
    }
}
