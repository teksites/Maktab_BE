namespace Stripe.Contracts;

public sealed class StripeProviderErrorResponse
{
    public bool Success { get; set; }
    public StripeProviderErrorDetail Error { get; set; } = new();
}

public sealed class StripeProviderErrorDetail
{
    public string Code { get; set; } = "stripe_error";
    public string Message { get; set; } = "Stripe could not process the request.";
    public string? Type { get; set; }
    public string? DeclineCode { get; set; }
    public bool Retryable { get; set; }
}
