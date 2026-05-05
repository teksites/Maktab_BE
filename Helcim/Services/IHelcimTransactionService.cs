using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;

namespace Helcim.Services
{
    public interface IHelcimTransactionService
    {
        Task<HelcimPayInitializeResponse> InitializePayment(InitiatePaymentRequest request);
        Task HandleWebhook(HelcimCardTransactionWebhookResponse webhook);
        Task AddTransactionDetails(AddHelcimTransactionDetails transactionDetails);
        Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode);
        Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId);
    }
}
