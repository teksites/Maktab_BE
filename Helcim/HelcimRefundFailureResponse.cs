namespace Helcim
{
    /// <summary>
    /// Safe error returned by refund endpoints. Raw Helcim responses are deliberately not exposed.
    /// </summary>
    public class HelcimRefundFailureResponse
    {
        public bool Success { get; set; }
        public string Error { get; set; } = string.Empty;
        public string ErrorSource { get; set; } = string.Empty;
        public bool IsUpstreamFailure { get; set; }
        public bool Retryable { get; set; }
    }
}
