namespace Helcim
{
    public class HelcimPaymentCompletionResponse
    {
        public bool Success { get; set; }
        public bool Duplicate { get; set; }
        public int InvoiceId { get; set; }
        public int TransactionId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public string PaymentFlow { get; set; } = string.Empty;
    }
}
