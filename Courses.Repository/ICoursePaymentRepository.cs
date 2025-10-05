using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface ICoursePaymentRepository
    {
        /// <summary>
        /// Adds a new payment for a student course transaction.
        /// </summary>
        Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment);

        /// <summary>
        /// Updates an existing payment.
        /// </summary>
        Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment);

        /// <summary>
        /// Deletes a payment. Can perform soft or hard delete.
        /// </summary>
        Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false);

        /// <summary>
        /// Retrieves a payment by its ID.
        /// </summary>
        Task<CoursePaymentResponse> GetPayment(Guid paymentId);

        /// <summary>
        /// Retrieves all payments for a given student course transaction.
        /// </summary>
        Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid studentCourseTransactionId);
    }
}
