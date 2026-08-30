using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Products;

/// <summary>
/// Everything type-specific about a product. Adding a fourth product type is a new implementation
/// plus one DI registration - the handlers and controllers do not change.
/// </summary>
public interface IProductPolicy
{
    ProductType Type { get; }

    /// <summary>Eligibility and holding limits for opening the product (e.g. SIPP age 18+, one ISA per tax year).</summary>
    DomainResult EnsureCanOpen(OpenProductContext context);

    /// <summary>Rules applied when an instruction arrives (e.g. ISA allowance, SIPP withdrawal age).</summary>
    DomainResult EnsureCanInstruct(InstructionContext context);

    /// <summary>Rules re-applied when an instruction settles, against the settlement-date tax year.</summary>
    DomainResult EnsureCanSettle(InstructionContext context);

    /// <summary>Side effects to apply once an instruction is accepted (e.g. GIA marks it reportable). No-op by default.</summary>
    void OnAccepted(InstructionContext context)
    {
    }
}
