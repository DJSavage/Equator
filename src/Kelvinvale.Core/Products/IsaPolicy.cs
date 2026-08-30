using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Products;

/// <summary>
/// Stocks and shares ISA. One per customer per tax year; subscriptions across a tax year cannot
/// exceed the annual allowance. An instruction that would breach is rejected whole (see README) -
/// the response carries the remaining headroom so the caller can resubmit a figure that fits.
/// </summary>
public sealed class IsaPolicy : IProductPolicy
{
    public ProductType Type => ProductType.Isa;

    public DomainResult EnsureCanOpen(OpenProductContext context)
    {
        var taxYear = TaxYear.Containing(context.Today);
        var alreadyHasOne = context.Customer.Products
            .Any(p => p.Type == ProductType.Isa && p.TaxYear == taxYear);

        return alreadyHasOne
            ? DomainResult.Fail(
                ProblemCodes.IsaOnePerTaxYear,
                $"Customer already holds an ISA for the {taxYear.Label} tax year.",
                new Dictionary<string, object?> { ["taxYear"] = taxYear.Label })
            : DomainResult.Success();
    }

    public DomainResult EnsureCanInstruct(InstructionContext context) => CheckAllowance(context);

    public DomainResult EnsureCanSettle(InstructionContext context) => CheckAllowance(context);

    private static DomainResult CheckAllowance(InstructionContext context)
    {
        if (context.Instruction.Type != InstructionType.Subscription)
        {
            return DomainResult.Success();
        }

        var taxYear = context.EffectiveTaxYear;
        var remaining = IsaAllowanceCalculator.RemainingPence(
            context.ExistingInstructions, taxYear, context.Instruction.Id);

        if (context.Instruction.AmountPence <= remaining)
        {
            return DomainResult.Success();
        }

        return DomainResult.Fail(
            ProblemCodes.IsaAllowanceExceeded,
            $"This subscription of {Pounds(context.Instruction.AmountPence)} would take the {taxYear.Label} " +
            $"ISA past its {Pounds(taxYear.AllowancePence)} annual allowance. {Pounds(Math.Max(0, remaining))} of allowance remains.",
            new Dictionary<string, object?>
            {
                ["taxYear"] = taxYear.Label,
                ["annualAllowancePence"] = taxYear.AllowancePence,
                ["remainingAllowancePence"] = Math.Max(0, remaining),
                ["requestedAmountPence"] = context.Instruction.AmountPence,
            });
    }

    private static string Pounds(long pence) => (pence / 100m).ToString("C", System.Globalization.CultureInfo.GetCultureInfo("en-GB"));
}
