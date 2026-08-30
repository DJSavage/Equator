using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Products;

/// <summary>
/// Works out how much ISA subscription headroom is left in a tax year. Counts subscriptions that
/// are <see cref="InstructionStatus.Received"/> or <see cref="InstructionStatus.Settled"/> - a
/// pending instruction still consumes headroom so two near-simultaneous instructions cannot each
/// pass and then jointly breach.
/// </summary>
public static class IsaAllowanceCalculator
{
    public static long SubscribedPence(
        IEnumerable<Instruction> instructions,
        TaxYear taxYear,
        Guid? excludeInstructionId = null)
    {
        return instructions
            .Where(i => excludeInstructionId is null || i.Id != excludeInstructionId)
            .Where(i => i.Type == InstructionType.Subscription
                        && i.TaxYear == taxYear
                        && i.Status is InstructionStatus.Received or InstructionStatus.Settled)
            .Sum(i => i.AmountPence);
    }

    public static long RemainingPence(
        IEnumerable<Instruction> instructions,
        TaxYear taxYear,
        Guid? excludeInstructionId = null)
    {
        return taxYear.AllowancePence - SubscribedPence(instructions, taxYear, excludeInstructionId);
    }
}
