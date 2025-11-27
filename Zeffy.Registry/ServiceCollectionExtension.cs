using Microsoft.Extensions.DependencyInjection;
using Zeffy.Implementation.Services;
using Zeffy.Repository;
using Zeffy.Repository.Implementation;
using Zeffy.Services;

namespace Zeffy.Registry
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddZeffyServices(this IServiceCollection services)
        {
            services.AddScoped<IZeffyTransactionService, ZeffyTransactionService>();
            services.AddScoped<IZeffyTransactionRepository, ZeffyTransactionRepository>();

            return services;
        }
    }
}
