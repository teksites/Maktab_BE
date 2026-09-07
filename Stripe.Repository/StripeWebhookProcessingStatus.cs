namespace Stripe.Repository;

public enum StripeWebhookProcessingStatus
{
    Processing = 1,
    Processed = 2,
    Ignored = 3,
    Failed = 4,
    Duplicate = 5
}
