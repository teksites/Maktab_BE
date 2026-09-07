using Stripe.Contracts;
using Stripe.Repository;
using Stripe.Services;

namespace Stripe.Implementation.Services;

public sealed class StripeWebhookService : IStripeWebhookService
{
    private static readonly TimeSpan ReservationTimeout = TimeSpan.FromMinutes(5);
    private readonly IStripePaymentService _payments;
    private readonly IStripeWebhookRepository _repository;
    private readonly IEnumerable<IStripeWebhookEventHandler> _handlers;

    public StripeWebhookService(IStripePaymentService payments, IStripeWebhookRepository repository, IEnumerable<IStripeWebhookEventHandler> handlers)
    {
        _payments = payments;
        _repository = repository;
        _handlers = handlers;
    }

    public async Task<StripeWebhookHandlingResult> HandleAsync(string rawBody, string signatureHeader, CancellationToken cancellationToken = default)
    {
        StripeWebhookEventResponse stripeEvent;
        try { stripeEvent = _payments.VerifyWebhook(rawBody, signatureHeader); }
        catch (StripeIntegrationException) { return new StripeWebhookHandlingResult { Status = StripeWebhookHandlingStatus.InvalidSignature }; }

        var now = DateTime.UtcNow;
        var reservation = await _repository.TryReserveAsync(new StripeWebhookReservation
        {
            EventId = stripeEvent.EventId, EventType = stripeEvent.EventType, LiveMode = stripeEvent.LiveMode,
            RawPayload = rawBody, SignatureHeader = signatureHeader, ReceivedAtUtc = now, StaleBeforeUtc = now - ReservationTimeout
        }).ConfigureAwait(false);

        var result = new StripeWebhookHandlingResult
        {
            Status = reservation switch
            {
                StripeWebhookReservationResult.Reserved => StripeWebhookHandlingStatus.Processed,
                StripeWebhookReservationResult.AlreadyProcessed => StripeWebhookHandlingStatus.Duplicate,
                _ => StripeWebhookHandlingStatus.RetryLater
            },
            EventId = stripeEvent.EventId, EventType = stripeEvent.EventType, RawPayload = rawBody
        };

        if (result.Status != StripeWebhookHandlingStatus.Processed)
            return result;

        try
        {
            foreach (var handler in _handlers)
                await handler.HandleAsync(result, cancellationToken).ConfigureAwait(false);

            await _repository.UpdateAsync(new StripeWebhookProcessingUpdate
            {
                EventId = result.EventId,
                Status = _handlers.Any() ? StripeWebhookProcessingStatus.Processed : StripeWebhookProcessingStatus.Ignored,
                UpdatedOnUtc = DateTime.UtcNow,
                ProcessedOnUtc = DateTime.UtcNow
            }).ConfigureAwait(false);
            return result;
        }
        catch (Exception exception)
        {
            await _repository.UpdateAsync(new StripeWebhookProcessingUpdate
            {
                EventId = result.EventId,
                Status = StripeWebhookProcessingStatus.Failed,
                ErrorMessage = exception.Message,
                UpdatedOnUtc = DateTime.UtcNow
            }).ConfigureAwait(false);
            throw;
        }
    }
}
