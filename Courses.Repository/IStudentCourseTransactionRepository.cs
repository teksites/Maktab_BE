using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;

namespace Courses.Repository
{
    public interface IStudentCourseTransactionRepository
    {
        // ----------------------------
        // Transactions
        // ----------------------------
        Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction);

        Task<StudentCourseTransactionResponse?> GetTransaction(Guid transactionId);

        Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction);

        Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions();

        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByFamily(Guid familyId);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByCourse(Guid courseId);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByEnrollment(Guid enrollmentId);

        // ----------------------------
        // Enrollments
        // ----------------------------
        Task<bool> AddEnrollmentsToTransaction(Guid transactionId, IEnumerable<Guid> enrollmentIds);

        Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsForTransaction(Guid transactionId);

        // ----------------------------
        // Payments
        // ----------------------------
        Task<IEnumerable<StudentCoursePaymentResponse>> GetPaymentsByFamilyAsync(Guid familyId);

        // ----------------------------
        // Pending amounts
        // ----------------------------
        Task<IEnumerable<PendingAmountResponse>> GetPendingAmountsReportAsync(
            Guid? instituteId = null,
            Guid? courseId = null,
            Guid? courseGroupId = null,
            Guid? familyId = null);

        Task<decimal> GetPendingAmountByInstitute(Guid instituteId);

        Task<decimal> GetPendingAmountByCourse(Guid courseId);

        Task<decimal> GetPendingAmountByCourseGroup(Guid courseGroupId);

        Task<decimal> GetPendingAmountByFamily(Guid familyId);

        // ----------------------------
        // Additional Queries
        // ----------------------------
        Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsPerCourseAsync(Guid courseId);
    }
}
