using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Products;

/// <summary>
/// General investment account. No allowance and no eligibility test. Gains are taxable, so every
/// accepted instruction is flagged <see cref="Instruction.Reportable"/> for downstream tax reporting.
/// </summary>
public sealed class GiaPolicy : IProductPolicy
{
    public ProductType Type => ProductType.Gia;

    public DomainResult EnsureCanOpen(OpenProductContext context) => DomainResult.Success();

    public DomainResult EnsureCanInstruct(InstructionContext context) => DomainResult.Success();

    public DomainResult EnsureCanSettle(InstructionContext context) => DomainResult.Success();

    public void OnAccepted(InstructionContext context) => context.Instruction.Reportable = true;
}
