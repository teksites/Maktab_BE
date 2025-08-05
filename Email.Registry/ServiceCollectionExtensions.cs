using Email;
using Email.Implementation;
using Microsoft.Extensions.DependencyInjection;

namespace Email.Registry
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddEmail(this IServiceCollection services)
        {
            services.AddSingleton<ISendEmailService, SendEmailService>();
            return services;
        }
    }
}
