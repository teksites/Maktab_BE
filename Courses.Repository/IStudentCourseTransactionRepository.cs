using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Repository
{
    public interface IStudentCourseTransactionRepository
    {
        Task<StudentCourseTransactionResponse> AddTransaction(AddStudentCourseTransaction transaction);
        Task<bool> DeleteTransaction(Guid transactionId, bool hardDelete = false);
        Task<IEnumerable<StudentCourseTransactionResponse>> GetAllTransactions(Guid courseId);
        Task<StudentCourseTransactionResponse> GetTransaction(Guid transactionId);
        Task<bool> UpdateTransaction(Guid transactionId, AddStudentCourseTransaction transaction);
    }
}
