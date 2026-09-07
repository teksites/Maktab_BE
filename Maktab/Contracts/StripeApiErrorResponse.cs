namespace Maktab.Contracts;

public sealed class StripeApiErrorResponse
{
    public bool Success { get; set; }
    public StripeApiErrorDetail Error { get; set; } = new();
}

public sealed class StripeApiErrorDetail
{
    public string Code { get; set; } = "stripe_error";
    public string Message { get; set; } = "Stripe could not process the request.";
    public string? Type { get; set; }
    public string? DeclineCode { get; set; }
    public bool Retryable { get; set; }
}
