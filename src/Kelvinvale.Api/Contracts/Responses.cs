using Kelvinvale.Core.Domain;

namespace Kelvinvale.Api.Contracts;

public sealed record CustomerResponse(
    Guid Id,
    Guid AdviserId,
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Email,
    string Address)
{
    public static CustomerResponse From(Customer c) =>
        new(c.Id, c.AdviserId, c.FirstName, c.LastName, c.DateOfBirth, c.Email, c.Address);
}

public sealed record ProductResponse(
    Guid Id,
    Guid CustomerId,
    string Type,
    DateTimeOffset OpenedAtUtc,
    string? TaxYear)
{
    public static ProductResponse From(Product p) =>
        new(p.Id, p.CustomerId, p.Type.ToString(), p.OpenedAtUtc, p.TaxYear?.Label);
}

public sealed record HoldingResponse(Guid Id, string FundCode, long ValuePence)
{
    public static HoldingResponse From(Holding h) => new(h.Id, h.FundCode, h.ValuePence);
}

public sealed record InstructionResponse(
    Guid Id,
    Guid ProductId,
    string Type,
    long AmountPence,
    string FundCode,
    string ClientReference,
    string Status,
    string TaxYear,
    DateOnly ValueDate,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SettledAtUtc,
    bool Reportable,
    string? RefusalCode)
{
    public static InstructionResponse From(Instruction i) => new(
        i.Id, i.ProductId, i.Type.ToString(), i.AmountPence, i.FundCode, i.ClientReference,
        i.Status.ToString(), i.TaxYear.Label, i.ValueDate, i.CreatedAtUtc, i.SettledAtUtc, i.Reportable, i.RefusalCode);
}

public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    Guid ActorId,
    string ActorRole,
    string Action,
    string SubjectType,
    Guid SubjectId,
    Guid? CustomerId,
    string Outcome,
    string? Detail,
    string? ChangesJson)
{
    public static AuditEntryResponse From(AuditEntry a) => new(
        a.Id, a.OccurredAtUtc, a.ActorId, a.ActorRole, a.Action, a.SubjectType, a.SubjectId,
        a.CustomerId, a.Outcome.ToString(), a.Detail, a.ChangesJson);
}
