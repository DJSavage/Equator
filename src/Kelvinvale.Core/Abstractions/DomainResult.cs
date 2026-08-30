namespace Kelvinvale.Core.Abstractions;

/// <summary>A business-rule failure: a stable machine <see cref="Code"/>, a human message, and optional detail members.</summary>
public sealed record DomainError(
    string Code,
    string Message,
    IReadOnlyDictionary<string, object?>? Extensions = null);

/// <summary>The outcome of evaluating a business rule.</summary>
public sealed class DomainResult
{
    private DomainResult(bool succeeded, DomainError? error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }

    public DomainError? Error { get; }

    public static DomainResult Success() => new(true, null);

    public static DomainResult Fail(DomainError error) => new(false, error);

    public static DomainResult Fail(string code, string message, IReadOnlyDictionary<string, object?>? extensions = null) =>
        new(false, new DomainError(code, message, extensions));
}
