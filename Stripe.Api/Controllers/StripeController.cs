using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe.Contracts;
using Stripe.Services;

namespace Stripe.Api.Controllers;

// Generic provider API: callers own the monetary/business validation and ReferenceData interpretation.
[ApiController]
[Authorize]
[TypeFilter(typeof(StripeExceptionFilter))]
[Route("api/stripe")]
public sealed class StripeController : ControllerBase
{
    private readonly IStripePaymentService _payments;
    private readonly IStripeWebhookService _webhooks;

    public StripeController(IStripePaymentService payments, IStripeWebhookService webhooks)
    {
        _payments = payments;
        _webhooks = webhooks;
    }

    [HttpPost("payment-intents")]
    public Task<StripePaymentIntentResponse> CreatePaymentIntent(StripePaymentIntentCreateRequest request, CancellationToken cancellationToken)
        => _payments.CreatePaymentIntentAsync(request, cancellationToken);

    [HttpGet("payment-intents/{paymentIntentId}")]
    public Task<StripePaymentIntentResponse> GetPaymentIntent(string paymentIntentId, CancellationToken cancellationToken)
        => _payments.GetPaymentIntentAsync(paymentIntentId, cancellationToken);

    [HttpPost("payment-intents/{paymentIntentId}/cancel")]
    public Task<StripePaymentIntentResponse> CancelPaymentIntent(string paymentIntentId, CancellationToken cancellationToken)
        => _payments.CancelPaymentIntentAsync(paymentIntentId, cancellationToken);

    [HttpPost("refunds")]
    public Task<StripeRefundResponse> CreateRefund(StripeRefundCreateRequest request, CancellationToken cancellationToken)
        => _payments.CreateRefundAsync(request, cancellationToken);

    [HttpGet("refunds/{refundId}")]
    public Task<StripeRefundResponse> GetRefund(string refundId, CancellationToken cancellationToken)
        => _payments.GetRefundAsync(refundId, cancellationToken);

    [HttpPost("reconcile/payment-intents/{paymentIntentId}")]
    public Task<StripePaymentIntentResponse> ReconcilePaymentIntent(string paymentIntentId, CancellationToken cancellationToken)
        => _payments.GetPaymentIntentAsync(paymentIntentId, cancellationToken);

    [HttpPost("reconcile/refunds/{refundId}")]
    public Task<StripeRefundResponse> ReconcileRefund(string refundId, CancellationToken cancellationToken)
        => _payments.GetRefundAsync(refundId, cancellationToken);

    [AllowAnonymous]
    [HttpPost("webhooks")]
    public async Task<IActionResult> Webhook(CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(Request.Body);
        var rawBody = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        var result = await _webhooks.HandleAsync(rawBody, Request.Headers["Stripe-Signature"].ToString(), cancellationToken).ConfigureAwait(false);
        return result.Status switch
        {
            StripeWebhookHandlingStatus.Processed => Ok(new { status = "processed" }),
            StripeWebhookHandlingStatus.Duplicate => Ok(new { status = "duplicate" }),
            StripeWebhookHandlingStatus.RetryLater => StatusCode(503, new { status = "retry_later" }),
            _ => Unauthorized(new { error = "Invalid Stripe signature" })
        };
    }
}
