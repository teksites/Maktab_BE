using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;
using MaktabDataContracts.Responses.Transactions;

namespace Courses.Services
{
    public interface IStudentCourseTransactionService
    {
        Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction);
        Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false);
        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions(Guid courseId);
        Task<StudentCourseTransactionResponse> GetTransaction(Guid transactionId);
        Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction);
    }
}
