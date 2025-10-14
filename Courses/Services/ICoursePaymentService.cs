using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface ICoursePaymentService
    {
        Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment);
        Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false);
        Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid courseId);
        Task<IEnumerable<CoursePaymentResponse>> GetAllPaymentsByStudentTransactionId(Guid studentTransactionId);
        Task<CoursePaymentResponse> GetPayment(Guid paymentId);
        Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment);
    }
}
