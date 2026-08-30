using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Products;

namespace Kelvinvale.Tests.Unit;

public sealed class ProductPolicyTests
{
    private static readonly DateOnly Today = new(2026, 8, 30);

    private static Customer CustomerAgedYears(int years) => new()
    {
        Id = Guid.NewGuid(),
        AdviserId = Guid.NewGuid(),
        FirstName = "Test",
        LastName = "Customer",
        Email = "t@example.com",
        Address = "1 Test St",
        DateOfBirth = Today.AddYears(-years),
    };

    private static InstructionContext InstructionOn(Product product, Customer customer, InstructionType type, DateOnly effectiveDate) => new()
    {
        Customer = customer,
        Product = product,
        Instruction = new Instruction
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Type = type,
            AmountPence = 100_000,
            FundCode = "GLB-EQ-ACC",
            ClientReference = "unit-0001",
            TaxYear = TaxYear.Containing(effectiveDate),
        },
        EffectiveDate = effectiveDate,
        EffectiveTaxYear = TaxYear.Containing(effectiveDate),
        ExistingInstructions = [],
    };

    [Theory]
    [InlineData(17, false)]
    [InlineData(18, true)]
    [InlineData(40, true)]
    public void Sipp_opening_needs_the_customer_to_be_18(int age, bool expectedSuccess)
    {
        var result = new SippPolicy().EnsureCanOpen(new OpenProductContext(CustomerAgedYears(age), ProductType.Sipp, Today));

        Assert.Equal(expectedSuccess, result.Succeeded);
        if (!expectedSuccess)
        {
            Assert.Equal(ProblemCodes.SippMinimumAge, result.Error!.Code);
        }
    }

    [Theory]
    [InlineData(54, false)]
    [InlineData(55, true)]
    public void Sipp_withdrawal_needs_the_minimum_pension_age(int age, bool expectedSuccess)
    {
        var customer = CustomerAgedYears(age);
        var product = new Product { Id = Guid.NewGuid(), CustomerId = customer.Id, Type = ProductType.Sipp };
        var context = InstructionOn(product, customer, InstructionType.Withdrawal, Today);

        var result = new SippPolicy().EnsureCanInstruct(context);

        Assert.Equal(expectedSuccess, result.Succeeded);
        if (!expectedSuccess)
        {
            Assert.Equal(ProblemCodes.SippWithdrawalAge, result.Error!.Code);
        }
    }

    [Fact]
    public void Sipp_subscription_has_no_age_gate()
    {
        var customer = CustomerAgedYears(30);
        var product = new Product { Id = Guid.NewGuid(), CustomerId = customer.Id, Type = ProductType.Sipp };

        var result = new SippPolicy().EnsureCanInstruct(InstructionOn(product, customer, InstructionType.Subscription, Today));

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Gia_marks_an_accepted_instruction_reportable()
    {
        var customer = CustomerAgedYears(30);
        var product = new Product { Id = Guid.NewGuid(), CustomerId = customer.Id, Type = ProductType.Gia };
        var context = InstructionOn(product, customer, InstructionType.Subscription, Today);

        new GiaPolicy().OnAccepted(context);

        Assert.True(context.Instruction.Reportable);
    }

    [Fact]
    public void Isa_opening_refuses_a_second_isa_in_the_same_tax_year()
    {
        var customer = CustomerAgedYears(30);
        customer.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            CustomerId = customer.Id,
            Type = ProductType.Isa,
            TaxYear = TaxYear.Containing(Today),
        });

        var result = new IsaPolicy().EnsureCanOpen(new OpenProductContext(customer, ProductType.Isa, Today));

        Assert.False(result.Succeeded);
        Assert.Equal(ProblemCodes.IsaOnePerTaxYear, result.Error!.Code);
    }
}
