using MaktabDataContracts.Requests.Zeffy;
using MaktabDataContracts.Responses.Zeffy;

namespace Zeffy.Services
{
    public interface IZeffyTransactionService
    {
        Task SaveZeffyTransaction(ZeffyRequest zeffy);
        Task<List<ZeffyResponse>> GetAllZeffyDonations();

        // Extra methods matching DB filters (optional but recommended)
        Task<IEnumerable<ZeffyResponse>> GetByStudentCourseTransactionId(Guid studentCourseTransactionId);
        Task<IEnumerable<ZeffyResponse>> GetByFamilyId(Guid familyId);
        Task<IEnumerable<ZeffyResponse>> GetByPaymentCode(string paymentCode);
        Task<IEnumerable<ZeffyResponse>> GetByFamilyAndPaymentCode(Guid familyId, string paymentCode);

        Task<ZeffyResponse?> GetByZeffyId(Guid zeffyId);
        Task<bool> Delete(Guid zeffyId, bool hardDelete = false);
        Task<bool> Update(ZeffyResponse zeffy);
    }
}
