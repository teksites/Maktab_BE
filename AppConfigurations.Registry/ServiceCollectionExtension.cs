using AppConfigurations.Implementation.Services;
using AppConfigurations.Repository;
using AppConfigurations.Repository.Implementation;
using AppConfigurations.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AppConfigurations.Registry
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddAppConfigurationsServices(this IServiceCollection services)
        {
            services.AddScoped<IAppConfigService, AppConfigService>();
            services.AddScoped<IAppConfigRepository, AppConfigRepository>();
            return services;
        }
    }
}
