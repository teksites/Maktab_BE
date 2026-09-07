using System;

namespace Maktab.Contracts;

public sealed class CreateStripeCoursePaymentIntentRequest
{
    public Guid StudentCourseTransactionId { get; set; }
    public decimal? Amount { get; set; }
    public string? ReceiptEmail { get; set; }
    // Supported Maktab options are card and acss_debit (Canadian pre-authorized debit).
    public string PaymentMethodType { get; set; } = "card";
    // Generated once by the client and reused only when retrying this same request.
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class CreateStripeRefundRequest
{
    public Guid StudentCourseTransactionId { get; set; }
    // Maktab accepts the PaymentIntent ID it recorded for the original Stripe credit.
    // The generic Stripe API still supports a Charge ID for non-Maktab consumers.
    public string PaymentIntentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Reason { get; set; }
    // Generated once by the admin client and reused only when retrying this same refund.
    public string IdempotencyKey { get; set; } = string.Empty;
}
