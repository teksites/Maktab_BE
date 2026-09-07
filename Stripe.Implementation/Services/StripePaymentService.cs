using Stripe.Configuration;
using Stripe.Contracts;
using Stripe.Services;
using Stripe.Repository;

namespace Stripe.Implementation.Services;

public class StripePaymentService : IStripePaymentService
{
    private readonly IStripeConfiguration _configuration;
    private readonly IStripePaymentIntentRepository? _paymentIntents;
    private readonly IStripeRefundRepository? _refunds;

    public StripePaymentService(IStripeConfiguration configuration)
        : this(configuration, null, null)
    {
    }

    public StripePaymentService(
        IStripeConfiguration configuration,
        IStripePaymentIntentRepository? paymentIntents,
        IStripeRefundRepository? refunds)
    {
        _configuration = configuration;
        _paymentIntents = paymentIntents;
        _refunds = refunds;
    }

    public async Task<StripePaymentIntentResponse> CreatePaymentIntentAsync(
        StripePaymentIntentCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidatePaymentIntentRequest(request);
        if (_paymentIntents != null)
        {
            var existing = await _paymentIntents.GetByIdempotencyKeyAsync(request.IdempotencyKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing?.StripePaymentIntentId))
                return await GetPaymentIntentAsync(existing.StripePaymentIntentId, cancellationToken).ConfigureAwait(false);
        }
        await ReservePaymentIntentAsync(request).ConfigureAwait(false);

        var options = new global::Stripe.PaymentIntentCreateOptions
        {
            Amount = request.AmountMinor,
            Currency = NormalizeCurrency(request.Currency),
            Customer = NormalizeOptional(request.CustomerId),
            ReceiptEmail = NormalizeOptional(request.ReceiptEmail),
            Description = NormalizeOptional(request.Description),
            PaymentMethodTypes = (request.PaymentMethodTypes?.Count ?? 0) == 0
                ? new List<string> { "card" }
                : request.PaymentMethodTypes!.Select(NormalizePaymentMethodType).ToList(),
            Metadata = BuildMetadata(request.Metadata, request.ReferenceData)
        };

        if (options.PaymentMethodTypes?.Contains("acss_debit", StringComparer.OrdinalIgnoreCase) == true)
        {
            options.PaymentMethodOptions = new global::Stripe.PaymentIntentPaymentMethodOptionsOptions
            {
                AcssDebit = new global::Stripe.PaymentIntentPaymentMethodOptionsAcssDebitOptions
                {
                    MandateOptions = new global::Stripe.PaymentIntentPaymentMethodOptionsAcssDebitMandateOptionsOptions
                    {
                        PaymentSchedule = "sporadic",
                        TransactionType = "personal",
                        IntervalDescription = "One-time school fee payment"
                    }
                }
            };
        }

        try
        {
            var paymentIntent = await new global::Stripe.PaymentIntentService(GetClient())
                .CreateAsync(options, CreateRequestOptions(request.IdempotencyKey), cancellationToken)
                .ConfigureAwait(false);

            var result = MapPaymentIntent(paymentIntent);
            await UpdatePaymentIntentAsync(request.IdempotencyKey, result).ConfigureAwait(false);
            return result;
        }
        catch (global::Stripe.StripeException exception)
        {
            await RecordPaymentIntentFailureAsync(request.IdempotencyKey, exception).ConfigureAwait(false);
            throw ToIntegrationException(exception);
        }
    }

    public async Task<StripePaymentIntentResponse> GetPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new ArgumentException("Stripe payment intent ID is required.", nameof(paymentIntentId));
        }

        try
        {
            var paymentIntent = await new global::Stripe.PaymentIntentService(GetClient())
                .GetAsync(paymentIntentId.Trim(), cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            var result = MapPaymentIntent(paymentIntent);
            await PersistPaymentIntentSnapshotAsync(result).ConfigureAwait(false);
            return result;
        }
        catch (global::Stripe.StripeException exception)
        {
            throw ToIntegrationException(exception);
        }
    }

    public async Task<StripePaymentIntentResponse> CancelPaymentIntentAsync(
        string paymentIntentId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            throw new ArgumentException("Stripe payment intent ID is required.", nameof(paymentIntentId));
        try
        {
            var paymentIntent = await new global::Stripe.PaymentIntentService(GetClient())
                .CancelAsync(paymentIntentId.Trim(), cancellationToken: cancellationToken).ConfigureAwait(false);
            return MapPaymentIntent(paymentIntent);
        }
        catch (global::Stripe.StripeException exception) { throw ToIntegrationException(exception); }
    }

    public async Task<StripeRefundResponse> CreateRefundAsync(
        StripeRefundCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRefundRequest(request);
        if (_refunds != null)
        {
            var existing = await _refunds.GetByIdempotencyKeyAsync(request.IdempotencyKey).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(existing?.StripeRefundId))
                return await GetRefundAsync(existing.StripeRefundId, cancellationToken).ConfigureAwait(false);
        }
        await ReserveRefundAsync(request).ConfigureAwait(false);

        var options = new global::Stripe.RefundCreateOptions
        {
            PaymentIntent = NormalizeOptional(request.PaymentIntentId),
            Charge = NormalizeOptional(request.ChargeId),
            Amount = request.AmountMinor,
            Reason = NormalizeOptional(request.Reason),
            Metadata = BuildMetadata(request.Metadata, request.ReferenceData)
        };

        try
        {
            var refund = await new global::Stripe.RefundService(GetClient())
                .CreateAsync(options, CreateRequestOptions(request.IdempotencyKey), cancellationToken)
                .ConfigureAwait(false);

            var result = new StripeRefundResponse
            {
                RefundId = refund.Id,
                Status = refund.Status,
                AmountMinor = refund.Amount,
                Currency = refund.Currency,
                PaymentIntentId = refund.PaymentIntentId,
                ChargeId = refund.ChargeId,
                FailureReason = refund.FailureReason
                ,ReferenceData = GetReferenceData(refund.Metadata)
                ,Metadata = refund.Metadata
            };
            await UpdateRefundAsync(request.IdempotencyKey, result).ConfigureAwait(false);
            return result;
        }
        catch (global::Stripe.StripeException exception)
        {
            await RecordRefundFailureAsync(request.IdempotencyKey, request, exception).ConfigureAwait(false);
            throw ToIntegrationException(exception);
        }
    }

    public StripeWebhookEventResponse VerifyWebhook(string rawBody, string stripeSignatureHeader)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            throw new ArgumentException("Stripe webhook payload is required.", nameof(rawBody));
        }

        if (string.IsNullOrWhiteSpace(stripeSignatureHeader))
        {
            throw new ArgumentException("Stripe-Signature header is required.", nameof(stripeSignatureHeader));
        }

        EnsureWebhookConfigured();

        try
        {
            var stripeEvent = global::Stripe.EventUtility.ConstructEvent(
                rawBody,
                stripeSignatureHeader,
                _configuration.WebhookSigningSecret);

            return new StripeWebhookEventResponse
            {
                EventId = stripeEvent.Id,
                EventType = stripeEvent.Type,
                LiveMode = stripeEvent.Livemode
            };
        }
        catch (global::Stripe.StripeException exception)
        {
            throw new StripeIntegrationException("Stripe webhook signature verification failed.", exception);
        }
    }

    public async Task<StripeRefundResponse> GetRefundAsync(string refundId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refundId)) throw new ArgumentException("Stripe refund ID is required.", nameof(refundId));
        try
        {
            var refund = await new global::Stripe.RefundService(GetClient()).GetAsync(refundId.Trim(), cancellationToken: cancellationToken).ConfigureAwait(false);
            var result = new StripeRefundResponse
            {
                RefundId = refund.Id, Status = refund.Status, AmountMinor = refund.Amount, Currency = refund.Currency,
                PaymentIntentId = refund.PaymentIntentId, ChargeId = refund.ChargeId, FailureReason = refund.FailureReason,
                ReferenceData = GetReferenceData(refund.Metadata), Metadata = refund.Metadata
            };
            await PersistRefundSnapshotAsync(result).ConfigureAwait(false);
            return result;
        }
        catch (global::Stripe.StripeException exception) { throw ToIntegrationException(exception); }
    }

    private global::Stripe.StripeClient GetClient()
    {
        if (!_configuration.Enabled)
        {
            throw new StripeIntegrationException("Stripe integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_configuration.SecretKey))
        {
            throw new StripeIntegrationException("Stripe SecretKey is not configured.");
        }

        return new global::Stripe.StripeClient(_configuration.SecretKey);
    }

    private void EnsureWebhookConfigured()
    {
        if (!_configuration.Enabled)
        {
            throw new StripeIntegrationException("Stripe integration is disabled.");
        }

        if (string.IsNullOrWhiteSpace(_configuration.WebhookSigningSecret))
        {
            throw new StripeIntegrationException("Stripe WebhookSigningSecret is not configured.");
        }
    }

    private static global::Stripe.RequestOptions CreateRequestOptions(string idempotencyKey)
        => new() { IdempotencyKey = idempotencyKey.Trim() };

    private StripePaymentIntentResponse MapPaymentIntent(global::Stripe.PaymentIntent paymentIntent)
        => new()
        {
            PaymentIntentId = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            PublishableKey = _configuration.PublishableKey,
            Status = paymentIntent.Status,
            AmountMinor = paymentIntent.Amount,
            AmountReceivedMinor = paymentIntent.AmountReceived,
            Currency = paymentIntent.Currency,
            CustomerId = paymentIntent.CustomerId,
            LatestChargeId = paymentIntent.LatestChargeId,
            LiveMode = paymentIntent.Livemode
            ,ReferenceData = GetReferenceData(paymentIntent.Metadata)
            ,Metadata = paymentIntent.Metadata
        };

    private static Dictionary<string, string>? BuildMetadata(IReadOnlyDictionary<string, string>? metadata, string? referenceData)
    {
        var result = metadata?.ToDictionary(item => item.Key, item => item.Value) ?? new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(referenceData))
        {
            if (referenceData.Length > 500) throw new ArgumentException("Stripe ReferenceData cannot exceed 500 characters.", nameof(referenceData));
            result["reference_data"] = referenceData;
        }
        return result.Count == 0 ? null : result;
    }

    private static string? GetReferenceData(IDictionary<string, string>? metadata)
        => metadata != null && metadata.TryGetValue("reference_data", out var value) ? value : null;

    private static void ValidatePaymentIntentRequest(StripePaymentIntentCreateRequest request)
    {
        if (request.AmountMinor <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.AmountMinor), "Stripe amount must be greater than zero.");
        }

        _ = NormalizeCurrency(request.Currency);
        EnsureIdempotencyKey(request.IdempotencyKey);
    }

    private static void ValidateRefundRequest(StripeRefundCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PaymentIntentId) == string.IsNullOrWhiteSpace(request.ChargeId))
        {
            throw new ArgumentException("Provide exactly one Stripe PaymentIntentId or ChargeId.");
        }

        if (request.AmountMinor is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(request.AmountMinor), "Stripe refund amount must be greater than zero when provided.");
        }

        EnsureIdempotencyKey(request.IdempotencyKey);
    }

    private static void EnsureIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("A Stripe idempotency key is required.", nameof(idempotencyKey));
        }
    }

    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.Length != 3 || !normalized.All(char.IsLetter))
        {
            throw new ArgumentException("Stripe currency must be a three-letter ISO currency code.", nameof(currency));
        }

        return normalized;
    }

    private static string NormalizePaymentMethodType(string paymentMethodType)
    {
        var normalized = paymentMethodType?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException("Stripe payment method types cannot contain empty values.", nameof(paymentMethodType));
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildProviderErrorMessage(global::Stripe.StripeException exception)
        => string.IsNullOrWhiteSpace(exception.StripeError?.Message)
            ? "Stripe request failed."
            : exception.StripeError.Message;

    private static StripeIntegrationException ToIntegrationException(global::Stripe.StripeException exception)
        => new(BuildProviderErrorMessage(exception), exception)
        {
            Code = exception.StripeError?.Code,
            ErrorType = exception.StripeError?.Type,
            DeclineCode = exception.StripeError?.DeclineCode,
            IsUpstreamFailure = exception.StripeError == null
        };

    private Task ReservePaymentIntentAsync(StripePaymentIntentCreateRequest request)
        => _paymentIntents?.ReserveAsync(new StripePaymentIntentRecord
        {
            IdempotencyKey = request.IdempotencyKey,
            ReferenceData = request.ReferenceData,
            AmountMinor = request.AmountMinor,
            Currency = NormalizeCurrency(request.Currency),
            StripeStatus = "creating"
        }) ?? Task.CompletedTask;

    private Task UpdatePaymentIntentAsync(string key, StripePaymentIntentResponse response)
        => _paymentIntents?.UpdateAsync(new StripePaymentIntentRecord
        {
            IdempotencyKey = key,
            StripePaymentIntentId = response.PaymentIntentId,
            StripeChargeId = response.LatestChargeId,
            AmountReceivedMinor = response.AmountReceivedMinor,
            StripeStatus = response.Status,
            IsLiveMode = response.LiveMode
        }) ?? Task.CompletedTask;

    private Task RecordPaymentIntentFailureAsync(string key, global::Stripe.StripeException exception)
        => _paymentIntents?.UpdateAsync(new StripePaymentIntentRecord
        {
            IdempotencyKey = key,
            StripeStatus = "failed",
            ErrorCode = exception.StripeError?.Code,
            ErrorMessage = BuildProviderErrorMessage(exception)
        }) ?? Task.CompletedTask;

    private Task ReserveRefundAsync(StripeRefundCreateRequest request)
        => _refunds?.ReserveAsync(new StripeRefundRecord
        {
            IdempotencyKey = request.IdempotencyKey,
            StripePaymentIntentId = NormalizeOptional(request.PaymentIntentId),
            StripeChargeId = NormalizeOptional(request.ChargeId),
            ReferenceData = request.ReferenceData,
            AmountMinor = request.AmountMinor!.Value,
            Currency = "cad",
            StripeStatus = "creating"
        }) ?? Task.CompletedTask;

    private Task UpdateRefundAsync(string key, StripeRefundResponse response)
        => _refunds?.UpdateAsync(new StripeRefundRecord
        {
            IdempotencyKey = key,
            StripeRefundId = response.RefundId,
            StripeStatus = response.Status,
            IsLiveMode = false,
            FailureReason = response.FailureReason
        }) ?? Task.CompletedTask;

    private Task RecordRefundFailureAsync(string key, StripeRefundCreateRequest request, global::Stripe.StripeException exception)
        => _refunds?.UpdateAsync(new StripeRefundRecord
        {
            IdempotencyKey = key,
            StripeStatus = "failed",
            FailureReason = BuildProviderErrorMessage(exception)
        }) ?? Task.CompletedTask;

    private async Task PersistPaymentIntentSnapshotAsync(StripePaymentIntentResponse response)
    {
        if (_paymentIntents == null) return;
        var existing = await _paymentIntents.GetByStripePaymentIntentIdAsync(response.PaymentIntentId).ConfigureAwait(false);
        if (existing == null) return;
        await UpdatePaymentIntentAsync(existing.IdempotencyKey, response).ConfigureAwait(false);
    }

    private async Task PersistRefundSnapshotAsync(StripeRefundResponse response)
    {
        if (_refunds == null) return;
        var existing = await _refunds.GetByStripeRefundIdAsync(response.RefundId).ConfigureAwait(false);
        if (existing == null) return;
        await UpdateRefundAsync(existing.IdempotencyKey, response).ConfigureAwait(false);
    }
}
