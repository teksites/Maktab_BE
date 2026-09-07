namespace Stripe.Contracts;

public enum StripeWebhookHandlingStatus { Processed, Duplicate, RetryLater, InvalidSignature }

public sealed class StripeWebhookHandlingResult
{
    public StripeWebhookHandlingStatus Status { get; set; }
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
}
