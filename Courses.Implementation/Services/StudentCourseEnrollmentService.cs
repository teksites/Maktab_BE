using Courses.Repository;
using Courses.Services;
using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Implementation.Services
{
    public class StudentCourseEnrollmentService : IStudentCourseEnrollmentService
    {
        private readonly IStudentCourseEnrollmentRepository _repository;
        private readonly IStudentCourseTransactionService _studentCourseTransactionService;

        public StudentCourseEnrollmentService(IStudentCourseEnrollmentRepository repository, IStudentCourseTransactionService studentCourseEnrollmentService)
        {
            _repository = repository;
            _studentCourseTransactionService = studentCourseEnrollmentService;
        }

        public async Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment)
        {
            var childEnrollment = await GetStudentCourseEnrollment(enrollment.ChildId, enrollment.CourseId).ConfigureAwait(false);

            // If child is not already registered to the same course
            if (childEnrollment is not null)
            { 
                
                var familyTransactions = await _studentCourseTransactionService
                    .GetTransactionByFamily(enrollment.FamilyId)
                    .ConfigureAwait(false);

                var latestTransaction = familyTransactions
                    .OrderByDescending(x => x.CreatedAt)
                    .FirstOrDefault();

                // Now add the transcation
                //if (latestTransaction is not null)// add logic to check the course session here
                //{
                // caclulate the discount if any
                //Calculate the payble fee
                // Get instution polucy for discount

                //     _studentCourseEnrollmentService.UpdateTransaction(new AddStudentCourseTransaction
                //     {
                //         AmountDiscounted = latestTransaction.AmountDiscounted,
                //         StudentCourseTransactionId = latestTransaction.StudentCourseTransactionId,

                //         FamilyId = latestTransaction.FamilyId,
                //         PaymentCode = latestTransaction.PaymentCode,
                //         //       StudentCourseEnrollmentId = latestTransaction.StudentCourseEnrollmentId
                //     });
                ////     latestTransaction.StudentCourseEnrollmentId
                // }

                // add serive to generate payment code and invoice id
                // in case no exisiting transaction found, create a new one
                // enrollment.TransactionId = latestTransaction.Id;

                return await _repository.AddEnrollment(enrollment).ConfigureAwait(false); 
            }

            return null;

        }
        public Task<StudentCourseEnrollmentResponse> GetEnrollment(Guid enrollmentId)
            => _repository.GetEnrollment(enrollmentId);
        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentByFamily(Guid familyId)
            => _repository.GetAllEnrollmentsByFamily(familyId);

        public Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId)
            => _repository.GetAllEnrollmentsByCourse(courseId);

        public Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment)
            => _repository.UpdateEnrollment(enrollmentId, enrollment);

        public Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false)
            => _repository.DeleteEnrollment(enrollmentId, hardDelete);
        public Task<StudentCourseEnrollmentResponse> GetStudentCourseEnrollment(Guid childId, Guid courseId)
            => _repository.GetStudentCourseEnrollment(childId, courseId);
    }
}
