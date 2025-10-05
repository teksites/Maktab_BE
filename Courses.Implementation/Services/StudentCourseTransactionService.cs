using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Implementation.Services
{
    public class StudentCourseTransactionService : IStudentCourseTransactionService
    {
        private readonly IStudentCourseTransactionRepository _repository;

        public StudentCourseTransactionService(IStudentCourseTransactionRepository repository)
        {
            _repository = repository;
        }

        public Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction)
            => _repository.AddTransaction(transaction);

        public Task<StudentCourseTransactionResponse> GetTransaction(Guid transactionId)
            => _repository.GetTransaction(transactionId);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions(Guid courseId)
            => _repository.GetAllTransactions(courseId);

        public Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
            => _repository.UpdateTransaction(transactionId, transaction);

        public Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
            => _repository.DeleteTransaction(transactionId, hardDelete);
    }
}
