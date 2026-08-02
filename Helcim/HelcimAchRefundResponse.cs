namespace Helcim
{
    public class HelcimAchRefundResponse
    {
        public bool Success { get; set; }
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
    }
}
