using Microsoft.Extensions.DependencyInjection;
//using Sedat.Implementation.Configuration;
using WebMsgSender.Implementation;

namespace WebMsgSender.Registry
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddWebMsgSender(this IServiceCollection services)
        {
            services.AddScoped<IWebMsgSenderService, WebMsgSenderService>();
            return services;
        }
    }
}
