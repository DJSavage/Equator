using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Products;

/// <summary>
/// Self-invested personal pension. The customer must be 18 or over to hold one, and no withdrawal
/// is allowed before the minimum pension age.
/// </summary>
public sealed class SippPolicy : IProductPolicy
{
    public const int MinimumHoldingAge = 18;

    // Currently 55; legislated to rise to 57 from April 2028. Left as a constant here and called
    // out in DECISIONS.md - a production system would source this from configuration / a rules table.
    public const int MinimumPensionAge = 55;

    public ProductType Type => ProductType.Sipp;

    public DomainResult EnsureCanOpen(OpenProductContext context)
    {
        var age = context.Customer.AgeOn(context.Today);
        return age >= MinimumHoldingAge
            ? DomainResult.Success()
            : DomainResult.Fail(
                ProblemCodes.SippMinimumAge,
                $"A SIPP can only be opened for a customer aged {MinimumHoldingAge} or over; this customer is {age}.",
                new Dictionary<string, object?> { ["minimumAge"] = MinimumHoldingAge, ["customerAge"] = age });
    }

    public DomainResult EnsureCanInstruct(InstructionContext context) => CheckWithdrawalAge(context);

    public DomainResult EnsureCanSettle(InstructionContext context) => CheckWithdrawalAge(context);

    private static DomainResult CheckWithdrawalAge(InstructionContext context)
    {
        if (context.Instruction.Type != InstructionType.Withdrawal)
        {
            return DomainResult.Success();
        }

        var age = context.Customer.AgeOn(context.EffectiveDate);
        return age >= MinimumPensionAge
            ? DomainResult.Success()
            : DomainResult.Fail(
                ProblemCodes.SippWithdrawalAge,
                $"No withdrawal is allowed from a SIPP before the minimum pension age of {MinimumPensionAge}; this customer is {age}.",
                new Dictionary<string, object?> { ["minimumPensionAge"] = MinimumPensionAge, ["customerAge"] = age });
    }
}
