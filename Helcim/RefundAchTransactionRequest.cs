namespace Helcim
{
    public class RefundAchTransactionRequest
    {
        public int TransactionId { get; set; }
        public decimal Amount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
    }
}
