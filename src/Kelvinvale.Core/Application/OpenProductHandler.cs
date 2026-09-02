using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;
using Kelvinvale.Core.Products;
using Microsoft.EntityFrameworkCore;

namespace Kelvinvale.Core.Application;

/// <summary>
/// Advisers open products for the customers they look after. The type-specific rules live in the
/// product policy; this handler is the same regardless of how many product types exist.
/// </summary>
public sealed class OpenProductHandler(
    KelvinvaleDbContext db,
    IProductPolicyResolver policies,
    IAuditWriter audit,
    TimeProvider clock)
{
    public async Task<Product> HandleAsync(Customer customer, ProductType type, ActorContext actor, CancellationToken ct)
    {
        //retrieve the policy for the provided product type
        var policy = policies.For(type); // throws DomainException(400) for an unknown type
        var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);

        //now check against the product's policy EnsureCanOpen method to see whether this product type can be opened for this customer
        var eligibility = policy.EnsureCanOpen(new OpenProductContext(customer, type, today));
        if (!eligibility.Succeeded)
        {
            audit.Record(AuditEvents.From(
                actor, AuditActions.ProductOpened, nameof(Product), Guid.Empty, customer.Id,
                AuditOutcome.Refused, detail: $"{type} refused: {eligibility.Error!.Code}"));
            await db.SaveChangesAsync(ct);
            throw new DomainException(eligibility.Error!);
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = type,
            OpenedAtUtc = clock.GetUtcNow(),
            TaxYear = type == ProductType.Isa ? TaxYear.Containing(today) : null,
        };

        db.Products.Add(product);
        audit.Record(AuditEvents.From(
            actor, AuditActions.ProductOpened, nameof(Product), product.Id, customer.Id,
            AuditOutcome.Accepted, detail: $"Adviser {actor.ActorId} opened a {type} for customer {customer.Id}."));

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (type == ProductType.Isa)
        {
            // The partial unique index is the backstop for a race the in-memory pre-check missed.
            throw new DomainException(new DomainError(
                ProblemCodes.IsaOnePerTaxYear,
                $"Customer already holds an ISA for the {product.TaxYear!.Value.Label} tax year.",
                new Dictionary<string, object?> { ["taxYear"] = product.TaxYear!.Value.Label }));
        }

        return product;
    }
}
