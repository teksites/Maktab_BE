using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;

namespace Courses.Services
{
    public interface IStudentCourseTransactionService
    {
        // ----------------------------
        // Transactions
        // ----------------------------
        Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction);

        Task<StudentCourseTransactionResponse?> GetTransaction(Guid transactionId);

        Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction);

        Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions();

        Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionByFamily(Guid familyId);

        // ----------------------------
        // Enrollments
        // ----------------------------
        Task<bool> AddEnrollmentsToTransaction(Guid studentCourseTransactionId, Guid studentCourseEnrollmentId);

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
            Guid? familyId = null,
            string? paymentCode = null);

        Task<decimal> GetPendingAmountByInstitute(Guid instituteId);

        Task<decimal> GetPendingAmountByCourse(Guid courseId);

        Task<decimal> GetPendingAmountByCourseGroup(Guid courseGroupId);

        Task<decimal> GetPendingAmountByFamily(Guid familyId);

        // ----------------------------
        // Additional Queries
        // ----------------------------
        Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsPerCourseAsync(Guid courseId);

        Task<StudentCourseTransactionResponse> GetTransactionByFamilyForCurrentSession(Guid familyId, Guid instituteId);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetInstituteTransactionsByFamily(Guid familyId, Guid instituteId);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetCourseTransactionsByFamily(Guid courseId, Guid familyId);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByCourse(Guid courseId);

        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByInstitute(Guid instituteId);
    }
}
