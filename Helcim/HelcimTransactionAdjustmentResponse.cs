using MaktabDataContracts.Enums.Helcim;

namespace Helcim
{
    public class HelcimTransactionAdjustmentResponse
    {
        public bool Success { get; set; }
        public string PaymentFlow { get; set; } = string.Empty;
        public int TransactionId { get; set; }
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Operation { get; set; } = string.Empty;
        public int? AdjustmentTransactionId { get; set; }
        public bool? BatchClosed { get; set; }
        public bool LocalAdjustmentRecorded { get; set; }
        public bool RequiresReconciliation { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
        public string RawResponse { get; set; } = string.Empty;
        public HelcimAchAuthorizationStatus? StatusAuth { get; set; }
        public HelcimAchClearingStatus? StatusClearing { get; set; }
        public HelcimAchBatchStatus? StatusBatch { get; set; }
    }
}
