using MaktabDataContracts.Enums.Helcim;

namespace Helcim.Repository
{
    public class UpdateHelcimWebhookProcessing
    {
        public string WebhookId { get; set; } = string.Empty;
        public HelcimWebhookProcessingStatus ProcessingStatus { get; set; }
        public string? InvoiceNumber { get; set; }
        public HelcimInvoiceStatus? InvoiceStatus { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime UpdatedOnUtc { get; set; }
        public DateTime? ProcessedOnUtc { get; set; }
    }
}
