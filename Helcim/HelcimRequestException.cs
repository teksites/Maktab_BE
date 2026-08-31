namespace Helcim
{
    public class HelcimRequestException : Exception
    {
        public HelcimRequestException(string message, bool isUpstreamFailure, string rawResponse = "")
            : base(message)
        {
            IsUpstreamFailure = isUpstreamFailure;
            RawResponse = rawResponse ?? string.Empty;
        }

        public bool IsUpstreamFailure { get; }
        public string RawResponse { get; }
    }
}
