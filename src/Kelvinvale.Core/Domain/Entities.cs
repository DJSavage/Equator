namespace Kelvinvale.Core.Domain;

/// <summary>A Kelvinvale adviser. Looks after a set of customers.</summary>
public class Adviser
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public ICollection<Customer> Customers { get; } = new List<Customer>();
}

/// <summary>
/// A Kelvinvale customer. Belongs to exactly one adviser (<see cref="AdviserId"/>), which is the
/// anchor for adviser-side ownership checks.
/// </summary>
public class Customer
{
    public Guid Id { get; set; }

    public Guid AdviserId { get; set; }

    public Adviser Adviser { get; set; } = null!;

    public required string FirstName { get; set; }

    public required string LastName { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public required string Email { get; set; }

    public required string Address { get; set; }

    public ICollection<Product> Products { get; } = new List<Product>();

    /// <summary>Whole years old on <paramref name="on"/>.</summary>
    public int AgeOn(DateOnly on)
    {
        var age = on.Year - DateOfBirth.Year;
        if (DateOfBirth > on.AddYears(-age))
        {
            age--;
        }

        return age;
    }
}

/// <summary>A product a customer holds. Behaviour (eligibility, allowances) lives in the product policies.</summary>
public class Product
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public ProductType Type { get; set; }

    public DateTimeOffset OpenedAtUtc { get; set; }

    /// <summary>Set for ISAs (the tax year the ISA belongs to); <c>null</c> for GIA and SIPP.</summary>
    public TaxYear? TaxYear { get; set; }

    public ICollection<Holding> Holdings { get; } = new List<Holding>();

    public ICollection<Instruction> Instructions { get; } = new List<Instruction>();
}

/// <summary>A fund position within a product.</summary>
public class Holding
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public required string FundCode { get; set; }

    public long ValuePence { get; set; }
}

/// <summary>
/// A customer's request to move money within a product they hold. Captured as
/// <see cref="InstructionStatus.Received"/>; the settlement pass books or rejects it.
/// </summary>
public class Instruction
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public InstructionType Type { get; set; }

    public long AmountPence { get; set; }

    public required string FundCode { get; set; }

    /// <summary>Client-supplied idempotency key, unique per product.</summary>
    public required string ClientReference { get; set; }

    public InstructionStatus Status { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>The date the instruction is expected to (or did) settle. Drives the tax year it books to.</summary>
    public DateOnly ValueDate { get; set; }

    public DateTimeOffset? SettledAtUtc { get; set; }

    public Guid CreatedByCallerId { get; set; }

    /// <summary>The tax year this instruction counts against. Re-stamped from the settlement date at settlement.</summary>
    public TaxYear TaxYear { get; set; }

    /// <summary>Set when <see cref="Status"/> is <see cref="InstructionStatus.Rejected"/>: the machine code of the rule that refused it.</summary>
    public string? RefusalCode { get; set; }

    /// <summary>GIA gains are taxable, so GIA instructions are flagged reportable when accepted.</summary>
    public bool Reportable { get; set; }
}

/// <summary>
/// An append-only record of a change: who did what, on whose account, when, and whether it was
/// accepted or refused. This is the answer to Kelvinvale compliance's question.
/// </summary>
public class AuditEntry
{
    public Guid Id { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }

    public Guid ActorId { get; set; }

    public string ActorRole { get; set; } = string.Empty;

    public required string Action { get; set; }

    public required string SubjectType { get; set; }

    public Guid SubjectId { get; set; }

    /// <summary>The customer whose records were touched, for compliance queries.</summary>
    public Guid? CustomerId { get; set; }

    public string? CorrelationId { get; set; }

    public AuditOutcome Outcome { get; set; }

    public string? Detail { get; set; }

    /// <summary>JSON before/after snapshot for updates.</summary>
    public string? ChangesJson { get; set; }
}
