namespace Stripe.Repository;

public sealed class StripeWebhookProcessingUpdate
{
    public string EventId { get; set; } = string.Empty;
    public StripeWebhookProcessingStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime UpdatedOnUtc { get; set; }
    public DateTime? ProcessedOnUtc { get; set; }
}
