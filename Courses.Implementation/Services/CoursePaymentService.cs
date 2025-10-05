using Courses.Repository;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services.Implementation
{
    public class CoursePaymentService : ICoursePaymentService
    {
        private readonly ICoursePaymentRepository _repository;

        public CoursePaymentService(ICoursePaymentRepository repository)
        {
            _repository = repository;
        }

        public async Task<CoursePaymentResponse> AddPayment(AddCoursePayment payment)
            => await _repository.AddPayment(payment);

        public async Task<CoursePaymentResponse> GetPayment(Guid paymentId)
            => await _repository.GetPayment(paymentId);

        public async Task<IEnumerable<CoursePaymentResponse>> GetAllPayments(Guid transactionId)
            => await _repository.GetAllPayments(transactionId);

        public async Task<bool> UpdatePayment(Guid paymentId, AddCoursePayment payment)
            => await _repository.UpdatePayment(paymentId, payment);

        public async Task<bool> DeletePayment(Guid paymentId, bool hardDelete = false)
            => await _repository.DeletePayment(paymentId, hardDelete);
    }
}
