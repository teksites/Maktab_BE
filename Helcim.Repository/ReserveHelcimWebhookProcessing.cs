using MaktabDataContracts.Enums.Helcim;

namespace Helcim.Repository
{
    public class ReserveHelcimWebhookProcessing
    {
        public string WebhookId { get; set; } = string.Empty;
        public HelcimWebhookEventType WebhookType { get; set; }
        public int TransactionId { get; set; }
        public long WebhookTimestamp { get; set; }
        public string RawRequest { get; set; } = string.Empty;
        public string SignatureHeader { get; set; } = string.Empty;
        public DateTime ReceivedAtUtc { get; set; }
        public DateTime StaleBeforeUtc { get; set; }
    }
}
