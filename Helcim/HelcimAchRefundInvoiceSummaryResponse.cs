using MaktabDataContracts.Enums.Helcim;

namespace Helcim
{
    public class HelcimAchRefundInvoiceSummaryResponse
    {
        public int InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PaymentCode { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerCode { get; set; } = string.Empty;
        public Guid MaktabTransactionId { get; set; } = Guid.Empty;
        public Guid FamilyId { get; set; } = Guid.Empty;
        public int TransactionId { get; set; }
        public int OriginalTransactionId { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal InvoiceAmountPaid { get; set; }
        public decimal TransactionAmount { get; set; }
        public HelcimCurrency Currency { get; set; }
        public HelcimInvoiceStatus InvoiceStatus { get; set; }
        public HelcimAchAuthorizationStatus StatusAuth { get; set; }
        public HelcimAchClearingStatus StatusClearing { get; set; }
        public HelcimAchBatchStatus StatusBatch { get; set; }
        public DateTime? InvoiceDateCreated { get; set; }
        public DateTime? InvoiceDateUpdated { get; set; }
        public DateTime? InvoiceDatePaid { get; set; }
        public DateTime? TransactionDateCreated { get; set; }
        public DateTime? TransactionDateClosed { get; set; }
        public bool IsRefundTransaction { get; set; }
        public bool IsSettled { get; set; }
        public bool IsRefundable { get; set; }
    }
}
