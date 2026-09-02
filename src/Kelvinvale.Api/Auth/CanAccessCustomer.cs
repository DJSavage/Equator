using System.Security.Claims;
using Kelvinvale.Core.Auth;
using Kelvinvale.Core.Domain;
using Microsoft.AspNetCore.Authorization;

namespace Kelvinvale.Api.Auth;

/// <summary>Resource-based requirement: the caller must own the <see cref="Customer"/> being reached.</summary>
public sealed class CanAccessCustomerRequirement : IAuthorizationRequirement;

/// <summary>
/// Evaluates <see cref="CustomerAccess"/> for the resolved customer. Registered once; every
/// customer-, product- and instruction-scoped endpoint calls it after loading the aggregate.
/// </summary>
public sealed class CanAccessCustomerHandler
    : AuthorizationHandler<CanAccessCustomerRequirement, Customer>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CanAccessCustomerRequirement requirement,
        Customer resource)
    {
        var callerId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var role = context.User.FindFirstValue(ClaimTypes.Role);

        //pass the callerId, Role and Customer object to CustomerAccess.IsAllowed to check whether the requested action is allowed
        if (Guid.TryParse(callerId, out var id) && CustomerAccess.IsAllowed(id, role, resource))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
