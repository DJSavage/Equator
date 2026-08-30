using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Domain;

namespace Kelvinvale.Core.Products;

public interface IProductPolicyResolver
{
    IProductPolicy For(ProductType type);

    bool IsKnown(ProductType type);
}

/// <summary>
/// Maps a <see cref="ProductType"/> to its policy from whatever <see cref="IProductPolicy"/>
/// implementations are registered. A new product type needs no change here.
/// </summary>
public sealed class ProductPolicyResolver : IProductPolicyResolver
{
    private readonly IReadOnlyDictionary<ProductType, IProductPolicy> _policies;

    public ProductPolicyResolver(IEnumerable<IProductPolicy> policies)
    {
        _policies = policies.ToDictionary(p => p.Type);
    }

    public bool IsKnown(ProductType type) => _policies.ContainsKey(type);

    public IProductPolicy For(ProductType type)
    {
        if (_policies.TryGetValue(type, out var policy))
        {
            return policy;
        }

        throw new DomainException(
            new DomainError(ProblemCodes.ProductUnknownType, $"'{type}' is not a product Kelvinvale offers."),
            statusCode: 400);
    }
}
