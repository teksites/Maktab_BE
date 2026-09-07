using Microsoft.Extensions.DependencyInjection;
using Stripe.Configuration;
using Stripe.Implementation.Configuration;
using Stripe.Implementation.Services;
using Stripe.Services;
using Stripe.Repository;
using Stripe.Repository.Implementation;
using StripeModuleConfiguration = Stripe.Implementation.Configuration.StripeConfiguration;

namespace Stripe.Registry;

public static class ServiceCollectionExtension
{
    public static IServiceCollection AddStripeServices(this IServiceCollection services)
    {
        services.AddSingleton<IStripeConfiguration, StripeModuleConfiguration>();
        services.AddScoped<IStripePaymentService, StripePaymentService>();
        services.AddScoped<IStripeWebhookService, StripeWebhookService>();
        services.AddScoped<IStripeWebhookRepository, StripeWebhookRepository>();
        services.AddScoped<IStripePaymentIntentRepository, StripePaymentIntentRepository>();
        services.AddScoped<IStripeRefundRepository, StripeRefundRepository>();
        return services;
    }
}
