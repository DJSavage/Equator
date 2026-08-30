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

public sealed class ProductsController(
    IAuthorizationService authorization,
    KelvinvaleDbContext db,
    PlaceInstructionHandler placeInstruction) : ApiControllerBase(authorization)
{
    /// <summary>View a single product.</summary>
    [HttpGet("products/{productId:guid}")]
    public async Task<ActionResult<ProductResponse>> GetProduct(Guid productId, CancellationToken ct)
    {
        var product = await Load(productId, ct, includeHoldings: false, includeInstructions: false);
        if (product is null || !await OwnsCustomerAsync(product.Customer))
        {
            return NotOwned();
        }

        return ProductResponse.From(product);
    }

    /// <summary>The fund positions within a product.</summary>
    [HttpGet("products/{productId:guid}/holdings")]
    public async Task<ActionResult<IReadOnlyList<HoldingResponse>>> GetHoldings(Guid productId, CancellationToken ct)
    {
        var product = await Load(productId, ct, includeHoldings: true, includeInstructions: false);
        if (product is null || !await OwnsCustomerAsync(product.Customer))
        {
            return NotOwned();
        }

        return product.Holdings.OrderBy(h => h.FundCode).Select(HoldingResponse.From).ToList();
    }

    /// <summary>The instructions placed against a product.</summary>
    [HttpGet("products/{productId:guid}/instructions")]
    public async Task<ActionResult<IReadOnlyList<InstructionResponse>>> GetInstructions(Guid productId, CancellationToken ct)
    {
        var product = await Load(productId, ct, includeHoldings: false, includeInstructions: true);
        if (product is null || !await OwnsCustomerAsync(product.Customer))
        {
            return NotOwned();
        }

        return product.Instructions.OrderBy(i => i.CreatedAtUtc).Select(InstructionResponse.From).ToList();
    }

    /// <summary>Customers place an instruction against a product they already hold.</summary>
    [HttpPost("products/{productId:guid}/instructions")]
    [Authorize(Policy = PolicyNames.IsCustomer)]
    public async Task<ActionResult<InstructionResponse>> PlaceInstruction(Guid productId, PlaceInstructionRequest request, CancellationToken ct)
    {
        var product = await db.Products
            .Include(p => p.Customer)
            .Include(p => p.Instructions)
            .FirstOrDefaultAsync(p => p.Id == productId, ct);

        if (product is null || !await OwnsCustomerAsync(product.Customer))
        {
            return NotOwned();
        }

        var type = RequestParsing.ParseEnum<InstructionType>(request.Type, "instruction.unknown-type", "instruction type");
        var command = new PlaceInstructionCommand(type, request.AmountPence, request.FundCode, request.ClientReference);
        var instruction = await placeInstruction.HandleAsync(product, command, Actor, ct);
        return Created($"/api/v1/products/{productId}/instructions", InstructionResponse.From(instruction));
    }

    private async Task<Product?> Load(Guid productId, CancellationToken ct, bool includeHoldings, bool includeInstructions)
    {
        IQueryable<Product> query = db.Products.AsNoTracking().Include(p => p.Customer);
        if (includeHoldings)
        {
            query = query.Include(p => p.Holdings);
        }

        if (includeInstructions)
        {
            query = query.Include(p => p.Instructions);
        }

        return await query.FirstOrDefaultAsync(p => p.Id == productId, ct);
    }
}
