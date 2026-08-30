using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Abstractions;

/// <summary>
/// Stages an audit record onto the current unit of work. The handler's <c>SaveChangesAsync</c>
/// commits the change and its audit entry together, so the trail can never drift from the data.
/// </summary>
public interface IAuditWriter
{
    void Record(AuditEntry entry);
}
