using Microsoft.Extensions.Configuration;
using Stripe.Configuration;

namespace Stripe.Implementation.Configuration;

public class StripeConfiguration : IStripeConfiguration
{
    public StripeConfiguration(IConfiguration configuration)
    {
        Enabled = bool.TryParse(configuration["Stripe:Enabled"], out var enabled) && enabled;
        SecretKey = configuration["Stripe:SecretKey"]?.Trim() ?? string.Empty;
        PublishableKey = configuration["Stripe:PublishableKey"]?.Trim() ?? string.Empty;
        WebhookSigningSecret = configuration["Stripe:WebhookSigningSecret"]?.Trim() ?? string.Empty;
    }

    public bool Enabled { get; }
    public string SecretKey { get; }
    public string PublishableKey { get; }
    public string WebhookSigningSecret { get; }
}
