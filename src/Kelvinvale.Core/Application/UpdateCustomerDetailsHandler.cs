using System.Text.Json;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;

namespace Kelvinvale.Core.Application;

public sealed record UpdateCustomerDetailsCommand(
    string FirstName,
    string LastName,
    string Email,
    string Address);

/// <summary>
/// Customers keep their own contact details up to date. Date of birth is treated as identity data
/// and is not editable here (see DECISIONS.md). Records a before/after snapshot for compliance.
/// </summary>
public sealed class UpdateCustomerDetailsHandler(KelvinvaleDbContext db, IAuditWriter audit)
{
    public async Task<Customer> HandleAsync(
        Customer customer,
        UpdateCustomerDetailsCommand command,
        ActorContext actor,
        CancellationToken ct)
    {
        var before = Snapshot(customer);

        customer.FirstName = command.FirstName.Trim();
        customer.LastName = command.LastName.Trim();
        customer.Email = command.Email.Trim();
        customer.Address = command.Address.Trim();

        var after = Snapshot(customer);

        audit.Record(AuditEvents.From(
            actor,
            AuditActions.CustomerDetailsUpdated,
            subjectType: nameof(Customer),
            subjectId: customer.Id,
            customerId: customer.Id,
            AuditOutcome.Accepted,
            detail: "Customer updated their personal details.",
            changesJson: JsonSerializer.Serialize(new { before, after })));

        await db.SaveChangesAsync(ct);
        return customer;
    }

    private static object Snapshot(Customer c) => new
    {
        c.FirstName,
        c.LastName,
        c.Email,
        c.Address,
    };
}
