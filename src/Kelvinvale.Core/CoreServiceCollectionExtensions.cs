using Kelvinvale.Core.Abstractions;
using Kelvinvale.Core.Application;
using Kelvinvale.Core.Products;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kelvinvale.Core;

public static class CoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the domain services: product policies, the resolver, the application handlers and
    /// the settlement runner. The host is responsible for registering <c>KelvinvaleDbContext</c>.
    /// </summary>
    public static IServiceCollection AddKelvinvaleCore(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IAuditWriter, AuditWriter>();

        // One line per product type. Adding a fourth type does not touch anything else.
        services.AddScoped<IProductPolicy, IsaPolicy>();
        services.AddScoped<IProductPolicy, GiaPolicy>();
        services.AddScoped<IProductPolicy, SippPolicy>();
        services.AddScoped<IProductPolicyResolver, ProductPolicyResolver>();

        services.AddScoped<CreateCustomerHandler>();
        services.AddScoped<UpdateCustomerDetailsHandler>();
        services.AddScoped<OpenProductHandler>();
        services.AddScoped<PlaceInstructionHandler>();
        services.AddScoped<ISettlementRunner, SettlementRunner>();

        return services;
    }
}
