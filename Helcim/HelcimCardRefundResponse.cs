namespace Helcim
{
    public class HelcimCardRefundResponse
    {
        public bool Success { get; set; }
        public int TransactionId { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool BatchClosed { get; set; }
        public string Operation { get; set; } = string.Empty;
        public int? AdjustmentTransactionId { get; set; }
        public bool LocalRefundRecorded { get; set; }
        public bool RequiresReconciliation { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
    }
}
