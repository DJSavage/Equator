using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;

namespace Kelvinvale.Core.Application;

public sealed record CreateCustomerCommand(
    string FirstName,
    string LastName,
    DateOnly DateOfBirth,
    string Email,
    string Address);

/// <summary>Advisers add new customers. The customer is assigned to the calling adviser.</summary>
public sealed class CreateCustomerHandler(KelvinvaleDbContext db, IAuditWriter audit)
{
    public async Task<Customer> HandleAsync(CreateCustomerCommand command, ActorContext actor, CancellationToken ct)
    {
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            AdviserId = actor.ActorId,
            FirstName = command.FirstName.Trim(),
            LastName = command.LastName.Trim(),
            DateOfBirth = command.DateOfBirth,
            Email = command.Email.Trim(),
            Address = command.Address.Trim(),
        };

        db.Customers.Add(customer);
        audit.Record(AuditEvents.From(
            actor,
            AuditActions.CustomerCreated,
            subjectType: nameof(Customer),
            subjectId: customer.Id,
            customerId: customer.Id,
            AuditOutcome.Accepted,
            detail: $"Adviser {actor.ActorId} created customer {customer.Id}."));

        await db.SaveChangesAsync(ct);
        return customer;
    }
}
