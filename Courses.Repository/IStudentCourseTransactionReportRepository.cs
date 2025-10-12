
using MaktabDataContracts.Responses.Transactions;

namespace Courses.Repository
{
    public interface IStudentCourseTransactionReportRepository
    {
        Task<IEnumerable<TransactionPendingFamilySummary>> GetPendingAmountByFamily(Guid? instituteId = null);
        Task<IEnumerable<TransactionPendingInstituteSummary>> GetPendingAmountByInstitute();
        Task<IEnumerable<TransactionPendingCourseSummary>> GetPendingAmountByCourse(Guid? instituteId = null);
        Task<IEnumerable<TransactionPendingCourseGroupSummary>> GetPendingAmountByCourseGroup(Guid? courseId = null);
        Task<IEnumerable<TransactionCollectedInstituteSummary>> GetTotalCollectedByInstitute(Guid? instituteId = null);
        Task<IEnumerable<TransactionPendingFamilySummary>> GetPendingAmountForFamily(Guid familyId);

        Task<IEnumerable<TransactionReportSummary>> GetTransactionsByFamily(Guid? familyId = null);
        Task<IEnumerable<CourseReportSummary>> GetTransactionsByCourse(Guid? instituteId = null, Guid? courseId = null);
        Task<IEnumerable<EnrollmentGroupReportSummary>> GetTransactionsByCourseGroup(Guid? courseId = null, Guid? groupId = null);
        Task<IEnumerable<TransactionReportSummary>> GetPendingTransactions(Guid? instituteId = null, Guid? courseId = null, Guid? familyId = null);
        Task<decimal> GetTotalPendingAmount(Guid? instituteId = null);
        Task<decimal> GetTotalCollectedAmount(Guid? instituteId = null);
    }
}
