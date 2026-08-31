namespace Helcim
{
    public class RefundCardTransactionRequest
    {
        public int TransactionId { get; set; }
        public decimal? Amount { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public string IdempotencyKey { get; set; } = string.Empty;
    }
}
