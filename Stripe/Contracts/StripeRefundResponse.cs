namespace Stripe.Contracts;

public class StripeRefundResponse
{
    public string RefundId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string? PaymentIntentId { get; set; }
    public string? ChargeId { get; set; }
    public string? FailureReason { get; set; }
    public string? ReferenceData { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
