namespace Stripe.Contracts;

public class StripeWebhookEventResponse
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public bool LiveMode { get; set; }
}
