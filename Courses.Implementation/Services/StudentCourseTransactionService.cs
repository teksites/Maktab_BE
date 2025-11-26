using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;
using System.Transactions;

namespace Courses.Implementation.Services
{
    public class StudentCourseTransactionService : IStudentCourseTransactionService
    {
        private readonly IStudentCourseTransactionRepository _repository;

        public StudentCourseTransactionService(IStudentCourseTransactionRepository repository)
        {
            _repository = repository;
        }

        // ----------------------------
        // Transactions
        // ----------------------------
        public Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
            => _repository.AddTransaction(transaction);

        public Task<StudentCourseTransactionResponse?> GetTransaction(Guid transactionId)
            => _repository.GetTransaction(transactionId);

        public Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
            => _repository.UpdateTransaction(transactionId, transaction);

        public Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
            => _repository.DeleteTransaction(transactionId, hardDelete);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions()
            => _repository.GetAllTransactions();

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionByFamily(Guid familyId)
            => _repository.GetTransactionByFamily(familyId);

        // ----------------------------
        // Enrollments
        // ----------------------------
        public Task<bool> AddEnrollmentsToTransaction(Guid studentCourseTransactionId, Guid studentCourseEnrollmentId)
            => _repository.AddEnrollmentsToTransaction(studentCourseTransactionId, studentCourseEnrollmentId);

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentsForTransaction(Guid transactionId)
            => _repository.GetEnrollmentsForTransaction(transactionId);

        // ----------------------------
        // Payments
        // ----------------------------
        public Task<IEnumerable<StudentCoursePaymentResponse>> GetPaymentsByFamilyAsync(Guid familyId)
            => _repository.GetPaymentsByFamilyAsync(familyId);

        // ----------------------------
        // Pending amounts
        // ----------------------------
        public Task<IEnumerable<PendingAmountResponse>> GetPendingAmountsReportAsync(
            Guid? instituteId = null,
            Guid? courseId = null,
            Guid? courseGroupId = null,
            Guid? familyId = null,
            string? paymentCode = null)
            => _repository.GetPendingAmountsReportAsync(instituteId, courseId, courseGroupId, familyId, paymentCode);

        public Task<decimal> GetPendingAmountByInstitute(Guid instituteId)
            => _repository.GetPendingAmountByInstitute(instituteId);

        public Task<decimal> GetPendingAmountByCourse(Guid courseId)
            => _repository.GetPendingAmountByCourse(courseId);

        public Task<decimal> GetPendingAmountByCourseGroup(Guid courseGroupId)
            => _repository.GetPendingAmountByCourseGroup(courseGroupId);

        public Task<decimal> GetPendingAmountByFamily(Guid familyId)
            => _repository.GetPendingAmountByFamily(familyId);

        // ----------------------------
        // Additional Queries
        // ----------------------------
        public Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionsPerCourseAsync(Guid courseId)
            => _repository.GetTransactionsPerCourseAsync(courseId);

        public Task<StudentCourseTransactionResponse> GetTransactionByFamilyForCurrentSession(Guid familyId, Guid instituteId)
            => _repository.GetTransactionByFamilyForCurrentSession(familyId, instituteId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetInstituteTransactionsByFamily(Guid familyId, Guid instituteId)
            => _repository.GetInstituteTransactionsByFamily(familyId, instituteId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetCourseTransactionsByFamily(Guid courseId, Guid familyId)
            => _repository.GetCourseTransactionsByFamily(courseId, familyId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByCourse(Guid courseId)
            => _repository.GetAllTransactionsByCourse(courseId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactionsByInstitute(Guid instituteId)
            => _repository.GetAllTransactionsByInstitute(instituteId);

        public Task<StudentCourseTransactionResponse?> GetTransactionByPaymentCode(string paymentCode)
        {
            return _repository.GetTransactionByPaymentCode(paymentCode);
        }

        public Task<bool> DeleteStudentCourseTransactionEnrollmentByEnrollmentId(Guid studentCourseEnrollmentId)
           => _repository.DeleteStudentCourseTransactionEnrollmentByEnrollmentId(studentCourseEnrollmentId);

        public Task<bool> DeleteStudentCourseTransactionEnrollmentByTransactionId(Guid transactionId)
           => _repository.DeleteStudentCourseTransactionEnrollmentByTransactionId(transactionId);

        public Task<bool> DeleteStudentCourseTransactionEnrollmentById(Guid id)
           => _repository.DeleteStudentCourseTransactionEnrollmentById(id);
    }
}
