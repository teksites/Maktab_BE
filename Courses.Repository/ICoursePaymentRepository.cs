using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface ICoursePaymentRepository
    {
        Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment);
        Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment);
        Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false);
        Task<CoursePaymentResponse?> GetPayment(Guid paymentId);
        Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByTransaction(Guid transactionId);
        Task<decimal> GetTotalPaidForStudentTransaction(Guid transactionId);
        Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid transactionId);
        Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByStudentTransactionId(Guid studentTransactionId);
    }
}
