namespace Stripe.Repository;

public sealed class StripeWebhookReservation
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public bool LiveMode { get; set; }
    public string SignatureHeader { get; set; } = string.Empty;
    public string RawPayload { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime StaleBeforeUtc { get; set; }
}
