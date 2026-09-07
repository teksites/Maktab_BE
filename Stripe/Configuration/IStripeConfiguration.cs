namespace Stripe.Configuration;

public interface IStripeConfiguration
{
    bool Enabled { get; }
    string SecretKey { get; }
    string PublishableKey { get; }
    string WebhookSigningSecret { get; }
}
