
using MaktabDataContracts.Responses.Transactions;

namespace Courses.Services
{
    public interface ICourseReportingService
    {
        Task<IEnumerable<PendingAmountPerFamily>> GetPendingAmountsPerFamily();
        Task<IEnumerable<PendingAmountPerInstitute>> GetPendingAmountsPerInstitute();
        Task<IEnumerable<PendingAmountPerCourse>> GetPendingAmountsPerCourse();
        Task<IEnumerable<PendingAmountPerCourseGroup>> GetPendingAmountsPerCourseGroup();
        Task<IEnumerable<TotalCollectedPerInstitute>> GetTotalPaymentsCollectedPerInstitute();
    }
}
