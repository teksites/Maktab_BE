using Stripe.Contracts;

namespace Stripe.Services;

public interface IStripePaymentService
{
    Task<StripePaymentIntentResponse> CreatePaymentIntentAsync(
        StripePaymentIntentCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<StripePaymentIntentResponse> GetPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<StripePaymentIntentResponse> CancelPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default);

    Task<StripeRefundResponse> CreateRefundAsync(
        StripeRefundCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<StripeRefundResponse> GetRefundAsync(
        string refundId,
        CancellationToken cancellationToken = default);

    StripeWebhookEventResponse VerifyWebhook(string rawBody, string stripeSignatureHeader);
}
