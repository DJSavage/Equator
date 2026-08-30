namespace Kelvinvale.Core.Abstractions;

/// <summary>
/// Who is making a change, passed from the API into the application handlers so the domain never
/// reaches into HTTP. Populated from the caller's claims.
/// </summary>
public sealed record ActorContext(Guid ActorId, string Role, string? CorrelationId)
{
    /// <summary>The actor for background work such as the settlement pass.</summary>
    public static ActorContext System { get; } = new(Guid.Empty, "System", CorrelationId: null);
}
