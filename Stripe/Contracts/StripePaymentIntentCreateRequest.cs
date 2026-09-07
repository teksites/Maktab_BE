namespace Stripe.Contracts;

public class StripePaymentIntentCreateRequest
{
    // Stripe requires monetary amounts in the currency's smallest unit, such as cents for CAD/USD.
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? CustomerId { get; set; }
    public string? ReceiptEmail { get; set; }
    public string? Description { get; set; }
    // Opaque application-owned correlation payload. Stripe stores it as metadata and returns it in webhooks.
    public string? ReferenceData { get; set; }
    public IReadOnlyCollection<string>? PaymentMethodTypes { get; set; }
    public IReadOnlyDictionary<string, string>? Metadata { get; set; }
}
