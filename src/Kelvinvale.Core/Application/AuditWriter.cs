using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;

namespace Kelvinvale.Core.Application;

/// <summary>Names of the audited actions. Constants so queries and tests do not rely on string literals.</summary>
public static class AuditActions
{
    public const string CustomerCreated = "Customer.Created";
    public const string CustomerDetailsUpdated = "Customer.DetailsUpdated";
    public const string ProductOpened = "Product.Opened";
    public const string InstructionPlaced = "Instruction.Placed";
    public const string InstructionRefused = "Instruction.Refused";
    public const string InstructionSettled = "Instruction.Settled";
    public const string InstructionRejectedAtSettlement = "Instruction.RejectedAtSettlement";
}

internal sealed class AuditWriter(KelvinvaleDbContext db, TimeProvider clock) : IAuditWriter
{
    public void Record(AuditEntry entry)
    {
        if (entry.Id == Guid.Empty)
        {
            entry.Id = Guid.NewGuid();
        }

        if (entry.OccurredAtUtc == default)
        {
            entry.OccurredAtUtc = clock.GetUtcNow();
        }

        db.AuditEntries.Add(entry);
    }
}

/// <summary>Builds <see cref="AuditEntry"/> instances from an <see cref="ActorContext"/> so handlers stay terse.</summary>
public static class AuditEvents
{
    public static AuditEntry From(
        ActorContext actor,
        string action,
        string subjectType,
        Guid subjectId,
        Guid? customerId,
        AuditOutcome outcome,
        string? detail = null,
        string? changesJson = null) =>
        new()
        {
            ActorId = actor.ActorId,
            ActorRole = actor.Role,
            CorrelationId = actor.CorrelationId,
            Action = action,
            SubjectType = subjectType,
            SubjectId = subjectId,
            CustomerId = customerId,
            Outcome = outcome,
            Detail = detail,
            ChangesJson = changesJson,
        };
}
