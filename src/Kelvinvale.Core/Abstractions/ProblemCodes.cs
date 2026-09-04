namespace Kelvinvale.Core.Abstractions;

/// <summary>
/// Stable machine-readable codes returned in the <c>code</c> member of a problem response and
/// stored on rejected instructions. Clients branch on these; keep them stable.
/// </summary>
public static class ProblemCodes
{
    public const string IsaAllowanceExceeded = "isa.allowance-exceeded";
    public const string IsaOnePerTaxYear = "isa.one-per-tax-year";
    public const string SippMinimumAge = "sipp.minimum-age";
    public const string SippWithdrawalAge = "sipp.withdrawal-age";
    public const string ProductUnknownType = "product.unknown-type";
    public const string InstructionInvalidAmount = "instruction.invalid-amount";
    public const string InstructionRefusedPreviously = "instruction.refused-previously";
    public const string AdviserUnknown = "adviser.unknown";
}
