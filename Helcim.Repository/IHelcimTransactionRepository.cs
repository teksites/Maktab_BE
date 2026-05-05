using MaktabDataContracts.Requests.Helcim;
using MaktabDataContracts.Responses.Helcim;

namespace Helcim.Repository
{
    public interface IHelcimTransactionRepository
    {
        Task Add(AddHelcimTransactionDetails transactionDetails);
        Task<List<HelcimTransactionResponse>> GetByPaymentCode(string paymentCode);
        Task<List<HelcimTransactionResponse>> GetByMaktabTransactionId(Guid maktabTransactionId);
        Task<List<HelcimTransactionResponse>> GetByTransactionId(int transactionId);
    }
}
