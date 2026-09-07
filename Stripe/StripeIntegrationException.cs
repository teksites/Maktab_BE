namespace Stripe;

public class StripeIntegrationException : Exception
{
    public string? Code { get; init; }
    public string? ErrorType { get; init; }
    public string? DeclineCode { get; init; }
    public bool IsUpstreamFailure { get; init; }
    public StripeIntegrationException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
