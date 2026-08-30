using Kelvinvale.Api.Auth;
using Kelvinvale.Api.Contracts;
using Kelvinvale.Core.Auth;
using Kelvinvale.Core.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kelvinvale.Api.Controllers;

/// <summary>
/// The compliance answer to "who changed what, on whose account, and when". Scoped to an adviser's
/// own customers. A production system would have a dedicated Compliance role (see DECISIONS.md).
/// </summary>
public sealed class AuditController(
    IAuthorizationService authorization,
    KelvinvaleDbContext db) : ApiControllerBase(authorization)
{
    [HttpGet("audit")]
    [Authorize(Policy = PolicyNames.IsAdviser)]
    public async Task<ActionResult<IReadOnlyList<AuditEntryResponse>>> GetAudit(
        [FromQuery] Guid customerId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null || !await OwnsCustomerAsync(customer))
        {
            return NotOwned();
        }

        var query = db.AuditEntries.AsNoTracking().Where(a => a.CustomerId == customerId);
        if (from is not null)
        {
            query = query.Where(a => a.OccurredAtUtc >= from);
        }

        if (to is not null)
        {
            query = query.Where(a => a.OccurredAtUtc <= to);
        }

        var entries = await query.OrderByDescending(a => a.OccurredAtUtc).ToListAsync(ct);
        return entries.Select(AuditEntryResponse.From).ToList();
    }
}
