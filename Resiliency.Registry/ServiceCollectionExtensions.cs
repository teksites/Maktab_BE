using Microsoft.Extensions.DependencyInjection;
using Polly;
using Resiliency.Implementation;

namespace Resiliency.Registry;
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddResiliency(this IServiceCollection services)
    {
        services.AddScoped<IResiliencyPipeline, PollyResiliencyPipeline>();
        return services;
    }
    public static IServiceCollection AddResiliencyPolicy(this IServiceCollection services, IAsyncPolicy policy)
    {
        services.AddScoped<IPolicyBuilder>(_ => new PollyAsyncPolicyBuilder(policy));
        return services;
    }
    public static IServiceCollection AddResiliencyPolicy(this IServiceCollection services, Func<IServiceProvider, IAsyncPolicy> implementationFactory)
    {
        services.AddScoped<IPolicyBuilder>(provider => new PollyAsyncPolicyBuilder(implementationFactory(provider)));
        return services;
    }
}