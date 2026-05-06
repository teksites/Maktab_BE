using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;

namespace Helcim.Repository
{
    public interface IHelcimTransactionRepository
    {
        Task Add(AddHelcimTransactionDetails transactionDetails);
        Task<HelcimWebhookReservationResult> TryReserveWebhookProcessing(ReserveHelcimWebhookProcessing webhookProcessing);
        Task UpdateWebhookProcessing(UpdateHelcimWebhookProcessing webhookProcessing);
        Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode);
        Task<List<HelcimTransactionResponseDetailed>> GetDetailedByPaymentCode(string paymentCode);
        Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId);
        Task<List<HelcimTransactionResponseDetailed>> GetDetailedByMaktabTransactionId(Guid maktabTransactionId);
        Task<List<HelcimTransactionResponse>> GetByTransactionId(int transactionId);
    }
}
