namespace Stripe.Contracts;

public class StripePaymentIntentResponse
{
    public string PaymentIntentId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    // Safe to expose to the client. It is required by Stripe.js with ClientSecret.
    public string PublishableKey { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public long AmountReceivedMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? LatestChargeId { get; set; }
    public bool LiveMode { get; set; }
    public string? ReferenceData { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
