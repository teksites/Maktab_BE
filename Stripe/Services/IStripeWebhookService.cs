using Stripe.Contracts;

namespace Stripe.Services;

public interface IStripeWebhookService
{
    Task<StripeWebhookHandlingResult> HandleAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken = default);
}
