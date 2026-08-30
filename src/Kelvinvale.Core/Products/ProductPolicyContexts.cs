using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Products;

/// <summary>Inputs for the "may this product be opened for this customer?" check.</summary>
public sealed record OpenProductContext(Customer Customer, ProductType Type, DateOnly Today);

/// <summary>
/// Inputs for evaluating an instruction, used both on arrival and at settlement. On arrival
/// <see cref="EffectiveDate"/> / <see cref="EffectiveTaxYear"/> are the value date's; at settlement
/// they are the settlement date's, so a boundary-crossing instruction is re-judged against the
/// year it actually books to.
/// </summary>
public sealed class InstructionContext
{
    public required Customer Customer { get; init; }

    public required Product Product { get; init; }

    public required Instruction Instruction { get; init; }

    public required DateOnly EffectiveDate { get; init; }

    public required TaxYear EffectiveTaxYear { get; init; }

    /// <summary>Every instruction already on the product (excluding <see cref="Instruction"/> itself).</summary>
    public required IReadOnlyList<Instruction> ExistingInstructions { get; init; }
}
