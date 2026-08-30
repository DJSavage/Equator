using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Auth;

/// <summary>Role names, as they arrive in the <c>X-Caller-Role</c> header / role claim.</summary>
public static class Roles
{
    public const string Adviser = "Adviser";
    public const string Customer = "Customer";
}

/// <summary>Authorization policy names used by the API.</summary>
public static class PolicyNames
{
    public const string IsAdviser = "IsAdviser";
    public const string IsCustomer = "IsCustomer";
}

/// <summary>
/// The one ownership rule in the system: <em>may this caller reach this customer's records?</em>
/// Every customer-, product- and instruction-scoped route resolves to a <see cref="Customer"/> and
/// asks this. Pure and side-effect free so it is trivially unit-testable from the wrong account.
/// </summary>
public static class CustomerAccess
{
    public static bool IsAllowed(Guid callerId, string? role, Customer customer) => role switch
    {
        Roles.Customer => callerId == customer.Id,
        Roles.Adviser => callerId == customer.AdviserId,
        _ => false,
    };
}
