using Helcim.Configuration;
using Helcim.Implementation.Configuration;
using Helcim.Implementation.Services;
using Helcim.Repository;
using Helcim.Repository.Implementation;
using Helcim.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Helcim.Registry
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddHelcimServices(this IServiceCollection services)
        {
            services.AddScoped<IHelcimTransactionService, HelcimTransactionService>();
            services.AddScoped<IHelcimTransactionRepository, HelcimTransactionRepository>();
            services.AddSingleton<IHelcimClientConfiguration, HelcimClientConfiguration>();

            return services;
        }
    }
}
