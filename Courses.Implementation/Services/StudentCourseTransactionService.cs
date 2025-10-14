using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Transactions;

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
            => _repository.GetAllTransactionsByCourse(courseId);

        public Task<StudentCourseTransactionResponse> GetTransactionByFamilyForCurrentSession(Guid familyId, Guid instituteId)
            => _repository.GetTransactionByFamilyForCurrentSession(familyId, instituteId);

        public Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction)
            => _repository.UpdateTransaction(transactionId, transaction);

        public Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false)
            => _repository.DeleteTransaction(transactionId, hardDelete);

        public Task<IEnumerable<StudentCourseTransactionResponse>> GetTransactionByFamily(Guid familyId)
        {
            return _repository.GetAllTransactionsByFamily(familyId);
        }
    }
}
