using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Core.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kelvinvale.Core.Application;

public interface ISettlementRunner
{
    /// <summary>Settles (or rejects) every received instruction whose value date has been reached. Returns the count processed.</summary>
    Task<int> RunDueAsync(CancellationToken ct);
}

/// <summary>
/// The authoritative allowance check. An instruction is re-judged against the tax year of its
/// settlement date, so an in-flight ISA subscription that crosses 6 April is booked to - and
/// checked against - the new tax year, and rejected there if it no longer fits.
/// </summary>
internal sealed class SettlementRunner(
    KelvinvaleDbContext db,
    IProductPolicyResolver policies,
    IAuditWriter audit,
    TimeProvider clock,
    ILogger<SettlementRunner> logger) : ISettlementRunner
{
    public async Task<int> RunDueAsync(CancellationToken ct)
    {
        var settlementDate = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        var due = await db.Instructions
            .Include(i => i.Product).ThenInclude(p => p.Customer)
            .Include(i => i.Product).ThenInclude(p => p.Instructions)
            .Include(i => i.Product).ThenInclude(p => p.Holdings)
            .Where(i => i.Status == InstructionStatus.Received && i.ValueDate <= settlementDate)
            .ToListAsync(ct);

        foreach (var instruction in due)
        {
            var taxYear = TaxYear.Containing(settlementDate);
            instruction.TaxYear = taxYear; // book to the tax year it settles in

            var policy = policies.For(instruction.Product.Type);
            var context = new InstructionContext
            {
                Customer = instruction.Product.Customer,
                Product = instruction.Product,
                Instruction = instruction,
                EffectiveDate = settlementDate,
                EffectiveTaxYear = taxYear,
                ExistingInstructions = instruction.Product.Instructions.Where(x => x.Id != instruction.Id).ToList(),
            };

            var check = policy.EnsureCanSettle(context);
            if (!check.Succeeded)
            {
                instruction.Status = InstructionStatus.Rejected;
                instruction.RefusalCode = check.Error!.Code;
                instruction.SettledAtUtc = clock.GetUtcNow();
                audit.Record(AuditEvents.From(
                    ActorContext.System,
                    AuditActions.InstructionRejectedAtSettlement,
                    subjectType: nameof(Instruction),
                    subjectId: instruction.Id,
                    customerId: instruction.Product.CustomerId,
                    AuditOutcome.Refused,
                    detail: $"Rejected at settlement against {taxYear.Label}: {check.Error!.Code}"));
                continue;
            }

            policy.OnAccepted(context);
            instruction.Status = InstructionStatus.Settled;
            instruction.SettledAtUtc = clock.GetUtcNow();
            ApplyToHoldings(instruction);
            audit.Record(AuditEvents.From(
                ActorContext.System,
                AuditActions.InstructionSettled,
                subjectType: nameof(Instruction),
                subjectId: instruction.Id,
                customerId: instruction.Product.CustomerId,
                AuditOutcome.Accepted,
                detail: $"Settled against {taxYear.Label}."));
        }

        if (due.Count > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Settlement pass processed {Count} instruction(s) as at {Date}.", due.Count, settlementDate);
        }

        return due.Count;
    }

    private static void ApplyToHoldings(Instruction instruction)
    {
        var holding = instruction.Product.Holdings.FirstOrDefault(h => h.FundCode == instruction.FundCode);
        switch (instruction.Type)
        {
            case InstructionType.Subscription:
                if (holding is null)
                {
                    instruction.Product.Holdings.Add(new Holding
                    {
                        Id = Guid.NewGuid(),
                        ProductId = instruction.ProductId,
                        FundCode = instruction.FundCode,
                        ValuePence = instruction.AmountPence,
                    });
                }
                else
                {
                    holding.ValuePence += instruction.AmountPence;
                }

                break;

            case InstructionType.Withdrawal:
                if (holding is not null)
                {
                    holding.ValuePence = Math.Max(0, holding.ValuePence - instruction.AmountPence);
                }

                break;

            case InstructionType.Switch:
                // A switch needs a target fund; modelling that is out of scope for this exercise (see README).
                break;
        }
    }
}
