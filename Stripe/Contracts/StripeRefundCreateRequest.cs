namespace Stripe.Contracts;

public class StripeRefundCreateRequest
{
    // Stripe requires exactly one of PaymentIntentId or ChargeId.
    public string? PaymentIntentId { get; set; }
    public string? ChargeId { get; set; }
    public long? AmountMinor { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? Reason { get; set; }
    // Opaque application-owned correlation payload. The Stripe module never parses this value.
    public string? ReferenceData { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
