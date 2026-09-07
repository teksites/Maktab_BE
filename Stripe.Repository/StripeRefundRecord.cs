namespace Stripe.Repository;

public sealed class StripeRefundRecord
{
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? StripeRefundId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public string? StripeChargeId { get; set; }
    public string? ReferenceData { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string StripeStatus { get; set; } = string.Empty;
    public bool IsLiveMode { get; set; }
    public string? FailureReason { get; set; }
}
