using MaktabDataContracts.Requests.Course;
using MaktabDataContracts.Responses.Course;

namespace Courses.Services
{
    public interface IStudentCourseEnrollmentService
    {
        Task<StudentCourseEnrollmentResponse> AddEnrollment(AddStudentCourseEnrollment enrollment);
        Task<bool> DeleteEnrollment(Guid enrollmentId, bool hardDelete = false);
        Task<IEnumerable<StudentCourseEnrollmentResponse>> GetAllEnrollments(Guid courseId);
        Task<StudentCourseEnrollmentResponse> GetEnrollment(Guid enrollmentId);
        Task<IEnumerable<StudentCourseEnrollmentResponse>> GetEnrollmentByFamily(Guid familyId);
        Task<bool> UpdateEnrollment(Guid enrollmentId, AddStudentCourseEnrollment enrollment);
    }
}
