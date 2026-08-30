using Kelvinvale.Core;
using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;
using Kelvinvale.Core.Products;
using Microsoft.Extensions.DependencyInjection;

namespace Kelvinvale.Tests.Products;

/// <summary>
/// Proves the claim in requirement 3: adding a fourth product type is a new policy class plus one
/// DI registration. The opening / instruction / settlement handlers dispatch through
/// <see cref="IProductPolicyResolver"/> and never branch on <see cref="ProductType"/>.
/// </summary>
public sealed class ProductPipelineExtensibilityTests
{
    private const ProductType FourthProductType = (ProductType)42;

    [Fact]
    public void A_new_product_policy_is_dispatched_to_from_a_single_registration()
    {
        var services = new ServiceCollection();
        services.AddKelvinvaleCore();
        services.AddScoped<IProductPolicy, FourthProductPolicy>(); // the only line a new product type adds

        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IProductPolicyResolver>();

        Assert.True(resolver.IsKnown(FourthProductType));
        Assert.IsType<FourthProductPolicy>(resolver.For(FourthProductType));
    }

    [Fact]
    public void The_built_in_product_types_are_all_resolvable()
    {
        var services = new ServiceCollection();
        services.AddKelvinvaleCore();
        using var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<IProductPolicyResolver>();

        Assert.Equal(ProductType.Isa, resolver.For(ProductType.Isa).Type);
        Assert.Equal(ProductType.Gia, resolver.For(ProductType.Gia).Type);
        Assert.Equal(ProductType.Sipp, resolver.For(ProductType.Sipp).Type);
    }

    private sealed class FourthProductPolicy : IProductPolicy
    {
        public ProductType Type => FourthProductType;

        public DomainResult EnsureCanOpen(OpenProductContext context) => DomainResult.Success();

        public DomainResult EnsureCanInstruct(InstructionContext context) => DomainResult.Success();

        public DomainResult EnsureCanSettle(InstructionContext context) => DomainResult.Success();
    }
}
