using MaktabDataContracts.Requests.Zeffy;
using MaktabDataContracts.Responses.Zeffy;

namespace Zeffy.Repository
{
    public interface IZeffyTransactionRepository
    {
        Task Add(AddZeffyRequest zeffy);

        Task<bool> Update(ZeffyResponse zeffy);

        Task<bool> Delete(Guid zeffyId, bool hardDelete = false);

        Task<ZeffyResponse?> GetByZeffyId(Guid zeffyId);

        Task<List<ZeffyResponse>> GetAllZeffyDonations();

        Task<IEnumerable<ZeffyResponse>> GetByStudentCourseTransactionId(Guid studentCourseTransactionId);

        Task<IEnumerable<ZeffyResponse>> GetByFamilyId(Guid familyId);

        Task<IEnumerable<ZeffyResponse>> GetByPaymentCode(string paymentCode);

        Task<IEnumerable<ZeffyResponse>> GetByFamilyAndPaymentCode(Guid familyId, string paymentCode);
    }
}
