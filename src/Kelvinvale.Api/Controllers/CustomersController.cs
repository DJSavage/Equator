using Kelvinvale.Api.Auth;
using Kelvinvale.Api.Contracts;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Application;
using Kelvinvale.Core.Auth;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Kelvinvale.Api.Controllers;

public sealed class CustomersController(
    IAuthorizationService authorization,
    KelvinvaleDbContext db,
    CreateCustomerHandler createCustomer,
    UpdateCustomerDetailsHandler updateCustomer,
    OpenProductHandler openProduct) : ApiControllerBase(authorization)
{
    /// <summary>Advisers add a new customer, assigned to themselves. returns 403 forbidden if role is not Adviser</summary>
    [HttpPost("customers")]
    [Authorize(Policy = PolicyNames.IsAdviser)]
    public async Task<ActionResult<CustomerResponse>> CreateCustomer(CreateCustomerRequest request, CancellationToken ct)
    {
        var command = new CreateCustomerCommand(request.FirstName, request.LastName, request.DateOfBirth, request.Email, request.Address);
        var customer = await createCustomer.HandleAsync(command, Actor, ct);
        return Created($"/api/v1/customers/{customer.Id}", CustomerResponse.From(customer));
    }

    /// <summary>View a single customer. Adviser who looks after them, or the customer themselves.
    /// No Authorize policy is enforced here, as both roles can call it.
    /// Ownership is the gate
    /// </summary>
    [HttpGet("customers/{customerId:guid}")]
    public async Task<ActionResult<CustomerResponse>> GetCustomer(Guid customerId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == customerId, ct);
        //check whether the caller can access the customer
        //customer can access their own record
        //adviser can access a customer they own
        if (customer is null || !await OwnsCustomerAsync(customer))
        {
            return NotOwned();
        }

        return CustomerResponse.From(customer);
    }

    /// <summary>Customers keep their own personal details up to date.</summary>
    /// [Authorize(Policy = PolicyNames.IsCustomer)] ensures only the customer role can access this endpoint
    [HttpPut("customers/{customerId:guid}")]
    [Authorize(Policy = PolicyNames.IsCustomer)]
    public async Task<ActionResult<CustomerResponse>> UpdateCustomer(Guid customerId, UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        //verify that the callerid matches the customerid being updated
        if (customer is null || !await OwnsCustomerAsync(customer))
        {
            return NotOwned();
        }

        var command = new UpdateCustomerDetailsCommand(request.FirstName, request.LastName, request.Email, request.Address);
        var updated = await updateCustomer.HandleAsync(customer, command, Actor, ct);
        return CustomerResponse.From(updated);
    }

    /// <summary>The products a customer holds.
    /// Can be viewed by Customer and Adviser
    /// No Authorize policy is enforced here, as both roles can call it.
    /// Ownership is the gate
    /// </summary>
    [HttpGet("customers/{customerId:guid}/products")]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> GetProducts(Guid customerId, CancellationToken ct)
    {
        var customer = await db.Customers.AsNoTracking()
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        if (customer is null || !await OwnsCustomerAsync(customer))
        {
            return NotOwned();
        }

        return customer.Products
            .OrderBy(p => p.OpenedAtUtc)
            .Select(ProductResponse.From)
            .ToList();
    }

    /// <summary>Advisers open a product (ISA / GIA / SIPP) for a customer they look after.
    /// Only advisers can access this endpoint. Any other role is given a 403 forbidden response
    /// </summary>
    [HttpPost("customers/{customerId:guid}/products")]
    [Authorize(Policy = PolicyNames.IsAdviser)]
    public async Task<ActionResult<ProductResponse>> OpenProduct(Guid customerId, OpenProductRequest request, CancellationToken ct)
    {
        //find the customer in the database
        var customer = await db.Customers
            .Include(c => c.Products)
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);

        //check for ownership
        if (customer is null || !await OwnsCustomerAsync(customer))
        {
            return NotOwned();
        }

        //parse the type to ensure it is a valid product
        var type = RequestParsing.ParseEnum<ProductType>(request.Type, ProblemCodes.ProductUnknownType, "product type");
        //try opening the product for the customer
        var product = await openProduct.HandleAsync(customer, type, Actor, ct);
        return Created($"/api/v1/products/{product.Id}", ProductResponse.From(product));
    }
}
