namespace Helcim.Services
{
    public enum HelcimWebhookHandlingStatus
    {
        Processed,
        Duplicate,
        RetryLater,
        Ignored,
        InvalidSignature,
        InvalidTimestamp
    }
}
