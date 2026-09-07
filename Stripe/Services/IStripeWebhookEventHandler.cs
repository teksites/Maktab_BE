using Stripe.Contracts;

namespace Stripe.Services;

// Applications register handlers for their own reference-data format and business effects.
public interface IStripeWebhookEventHandler
{
    Task HandleAsync(StripeWebhookHandlingResult stripeEvent, CancellationToken cancellationToken = default);
}
