using Kelvinvale.Core.Auth;
using Kelvinvale.Core.Domain;

namespace Kelvinvale.Tests.Unit;

public sealed class CustomerAccessTests
{
    private static readonly Guid AdviserId = Guid.NewGuid();
    private static readonly Guid OtherAdviserId = Guid.NewGuid();

    private static readonly Customer Customer = new()
    {
        Id = Guid.NewGuid(),
        AdviserId = AdviserId,
        FirstName = "Test",
        LastName = "Customer",
        Email = "t@example.com",
        Address = "1 Test St",
    };

    [Fact]
    public void A_customer_reaches_their_own_record()
    {
        Assert.True(CustomerAccess.IsAllowed(Customer.Id, Roles.Customer, Customer));
    }

    [Fact]
    public void A_customer_cannot_reach_another_customers_record()
    {
        Assert.False(CustomerAccess.IsAllowed(Guid.NewGuid(), Roles.Customer, Customer));
    }

    [Fact]
    public void An_adviser_reaches_a_customer_they_look_after()
    {
        Assert.True(CustomerAccess.IsAllowed(AdviserId, Roles.Adviser, Customer));
    }

    [Fact]
    public void An_adviser_cannot_reach_a_customer_they_do_not_look_after()
    {
        Assert.False(CustomerAccess.IsAllowed(OtherAdviserId, Roles.Adviser, Customer));
    }

    [Fact]
    public void An_advisers_id_matching_the_customer_id_under_the_customer_role_is_still_denied()
    {
        // Guards against confusing "is this my id" with "is this my customer".
        Assert.False(CustomerAccess.IsAllowed(AdviserId, Roles.Customer, Customer));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Superuser")]
    public void An_unknown_or_missing_role_is_denied(string? role)
    {
        Assert.False(CustomerAccess.IsAllowed(Customer.Id, role, Customer));
        Assert.False(CustomerAccess.IsAllowed(AdviserId, role, Customer));
    }
}
