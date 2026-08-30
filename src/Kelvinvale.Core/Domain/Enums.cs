namespace Kelvinvale.Core.Domain;

/// <summary>
/// The products Kelvinvale offers. Integer values are persisted and referenced by the
/// partial unique index that enforces "one ISA per customer per tax year" at the database,
/// so they must stay stable.
/// </summary>
public enum ProductType
{
    Isa = 0,
    Gia = 1,
    Sipp = 2,
}

/// <summary>A customer's request to move money within a product they hold.</summary>
public enum InstructionType
{
    Subscription = 0,
    Withdrawal = 1,
    Switch = 2,
}

/// <summary>
/// Instruction lifecycle. An instruction is captured as <see cref="Received"/>, then the
/// settlement pass moves it to <see cref="Settled"/> or <see cref="Rejected"/>.
/// </summary>
public enum InstructionStatus
{
    Received = 0,
    Settled = 1,
    Rejected = 2,
}

public enum AuditOutcome
{
    Accepted = 0,
    Refused = 1,
}
