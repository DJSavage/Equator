using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Core.Products;

namespace Kelvinvale.Core.Application;

public sealed record PlaceInstructionCommand(
    InstructionType Type,
    long AmountPence,
    string FundCode,
    string ClientReference);

/// <summary>
/// Customers place an instruction against a product they already hold. The instruction is checked
/// on arrival by the product policy; a refusal is persisted as a <see cref="InstructionStatus.Rejected"/>
/// instruction (so compliance sees the attempt) and surfaced as a 422.
/// </summary>
public sealed class PlaceInstructionHandler(
    KelvinvaleDbContext db,
    IProductPolicyResolver policies,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Instruction> HandleAsync(
        Product product,
        PlaceInstructionCommand command,
        ActorContext actor,
        CancellationToken ct)
    {
        // Idempotency: a replay of the same client reference yields the same outcome.
        var existing = product.Instructions.FirstOrDefault(i => i.ClientReference == command.ClientReference);
        //just return the existing Instruction if not null and not status Rejected
        if (existing is not null)
        {
            if (existing.Status == InstructionStatus.Rejected)
            {
                throw new DomainException(new DomainError(
                    existing.RefusalCode ?? ProblemCodes.InstructionRefusedPreviously,
                    "This instruction was refused when it was first submitted."));
            }

            return existing;
        }


        if (command.AmountPence <= 0)
        {
            throw new DomainException(
                new DomainError(ProblemCodes.InstructionInvalidAmount, "amountPence must be greater than zero."),
                statusCode: 400);
        }

        var now = clock.GetUtcNow();
        var arrivalDate = DateOnly.FromDateTime(now.UtcDateTime);
        var valueDate = SettlementCalendar.ValueDateFrom(arrivalDate);

        var instruction = new Instruction
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Type = command.Type,
            AmountPence = command.AmountPence,
            FundCode = command.FundCode.Trim(),
            ClientReference = command.ClientReference.Trim(),
            Status = InstructionStatus.Received,
            CreatedAtUtc = now,
            ValueDate = valueDate,
            CreatedByCallerId = actor.ActorId,
            TaxYear = TaxYear.Containing(valueDate),
        };

        var context = new InstructionContext
        {
            Customer = product.Customer,
            Product = product,
            Instruction = instruction,
            EffectiveDate = valueDate,
            EffectiveTaxYear = instruction.TaxYear,
            ExistingInstructions = product.Instructions.ToList(),
        };

        var check = policies.For(product.Type).EnsureCanInstruct(context);
        if (!check.Succeeded)
        {
            instruction.Status = InstructionStatus.Rejected;
            instruction.RefusalCode = check.Error!.Code;
            db.Instructions.Add(instruction);
            audit.Record(AuditEvents.From(
                actor, AuditActions.InstructionRefused, nameof(Instruction), instruction.Id, product.CustomerId,
                AuditOutcome.Refused, detail: $"{command.Type} on {product.Type} refused: {check.Error!.Code}"));
            await db.SaveChangesAsync(ct);
            throw new DomainException(check.Error!);
        }

        policies.For(product.Type).OnAccepted(context);
        db.Instructions.Add(instruction);
        audit.Record(AuditEvents.From(
            actor, AuditActions.InstructionPlaced, nameof(Instruction), instruction.Id, product.CustomerId,
            AuditOutcome.Accepted,
            detail: $"{command.Type} of {command.AmountPence}p on {product.Type} {product.Id}; value date {valueDate:yyyy-MM-dd}."));
        await db.SaveChangesAsync(ct);
        return instruction;
    }
}
