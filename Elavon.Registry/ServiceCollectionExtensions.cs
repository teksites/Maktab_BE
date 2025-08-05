using Elavon.Configuration;
using Elavon.Implementation.Configuration;
using Elavon.Implementation.Services;
using Elavon.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Elavon.Registry
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddElavonServices(this IServiceCollection services)
        {
            services.AddScoped<IElavonService, ElavonService>()
            .AddSingleton<IElavonClientConfiguration, ElavonClientConfiguration>();
            return services;
        }
    }
}
