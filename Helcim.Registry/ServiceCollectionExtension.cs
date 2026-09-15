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
            services.AddScoped<IHelcimCardVaultRepository, HelcimCardVaultRepository>();
            services.AddScoped<IHelcimPaymentAttemptRepository, HelcimPaymentAttemptRepository>();
            services.AddScoped<IHelcimCheckoutContextRepository, HelcimCheckoutContextRepository>();
            services.AddSingleton<IHelcimClientConfiguration, HelcimClientConfiguration>();
            services.AddSingleton<IHelcimCardVaultConfiguration, HelcimCardVaultConfiguration>();
            services.AddSingleton<IHelcimCardTokenProtector, HelcimCardTokenProtector>();
            services.AddSingleton<ICardBinCheckConfiguration, CardBinCheckConfiguration>();
            services.AddMemoryCache();
            services.AddHttpClient<ICardBinLookupService, CardBinLookupService>((serviceProvider, client) =>
            {
                var configuration = serviceProvider.GetRequiredService<ICardBinCheckConfiguration>();
                client.BaseAddress = new Uri(configuration.BaseUrl);
                client.Timeout = configuration.RequestTimeout;
            });

            return services;
        }
    }
}
