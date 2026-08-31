namespace Helcim
{
    public class RefundAchInvoiceRequest
    {
        public int InvoiceId { get; set; }
        public decimal? Amount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
    }
}
