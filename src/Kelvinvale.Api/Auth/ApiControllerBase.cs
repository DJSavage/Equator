using System.Security.Claims;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kelvinvale.Api.Auth;

/// <summary>
/// Base for the v1 controllers. Everything requires an authenticated caller by default; ownership
/// is checked per action via <see cref="OwnsCustomerAsync"/> once the target customer is loaded.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/v1")]
public abstract class ApiControllerBase(IAuthorizationService authorization) : ControllerBase
{
    protected Guid CallerId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    protected string CallerRole => User.FindFirstValue(ClaimTypes.Role)!;

    protected ActorContext Actor => new(CallerId, CallerRole, HttpContext.Items["CorrelationId"] as string);

    /// <summary>True if the caller owns this customer's records (adviser who looks after them, or the customer themselves).</summary>
    protected async Task<bool> OwnsCustomerAsync(Customer customer)
    {
        var result = await authorization.AuthorizeAsync(User, customer, new CanAccessCustomerRequirement());
        return result.Succeeded;
    }

    /// <summary>
    /// Returned when a record is not the caller's. Deliberately a 404, not a 403: a customer should
    /// not be able to tell whether another customer's ISA exists (see DECISIONS.md).
    /// </summary>
    protected NotFoundObjectResult NotOwned() => NotFound(new ProblemDetails
    {
        Status = StatusCodes.Status404NotFound,
        Title = "Not found.",
        Detail = "No such record, or it is not yours to access.",
    });
}
