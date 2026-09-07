namespace Stripe.Repository;

public sealed class StripePaymentIntentRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? StripePaymentIntentId { get; set; }
    public string? StripeChargeId { get; set; }
    public string? ReferenceData { get; set; }
    public long AmountMinor { get; set; }
    public long AmountReceivedMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string StripeStatus { get; set; } = string.Empty;
    public bool IsLiveMode { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
}
